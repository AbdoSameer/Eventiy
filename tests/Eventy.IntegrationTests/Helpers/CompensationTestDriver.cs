using Application.Abstractions.Payments;
using Application.Abstractions.Persistence;
using Domain.Common;

namespace Eventy.IntegrationTests.Helpers;

/// <summary>
/// Manual driver for the compensation pipeline. The real
/// <c>CompensationProcessor</c> is a <see cref="Microsoft.Extensions.Hosting.BackgroundService"/>
/// that polls every 10s with a 20s startup delay — far too slow and nondeterministic
/// for tests. This driver runs ONE processing cycle on demand, mirroring the
/// processor's logic so tests can assert on retry/backoff/dead-letter behaviour
/// deterministically.
/// </summary>
public sealed class CompensationTestDriver
{
    private readonly IServiceProvider _rootServices;
    private readonly Guid _testLockId = Guid.NewGuid();

    public CompensationTestDriver(IServiceProvider rootServices) => _rootServices = rootServices;

    /// <summary>
    /// Runs a single compensation-processing cycle: locks a batch, dispatches
    /// each log, marks results. Returns a summary of what happened.
    /// </summary>
    public async Task<CompensationCycleResult> ProcessOnceAsync(CancellationToken ct = default)
    {
        using var scope = _rootServices.CreateScope();
        var compensationRepo = scope.ServiceProvider.GetRequiredService<ICompensationLogRepository>();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var now = timeProvider.GetUtcNow().UtcDateTime;

        var pending = await compensationRepo.GetAndLockUnprocessedAsync(
            _testLockId, timeProvider, batchSize: 50, ct);

        var result = new CompensationCycleResult();

        foreach (var log in pending)
        {
            try
            {
                var dispatch = await DispatchAsync(paymentService, log, ct);

                if (dispatch.IsSuccess)
                {
                    result.ProcessedIds.Add(log.Id);
                    result.ProcessedByBooking[log.BookingId] = log.Id;
                }
                else
                {
                    var newRetry = log.RetryCount + 1;
                    var nextRetry = ComputeNextRetry(newRetry, now);
                    var error = string.Join("; ", dispatch.Errors.Select(e => e.Message));

                    if (newRetry >= 5)
                    {
                        await compensationRepo.MoveToDeadLetterAsync(log.Id, error, now, ct);
                        result.DeadLetteredIds.Add(log.Id);
                    }
                    else
                    {
                        result.Failed.Add((log.Id, error, newRetry, nextRetry));
                    }
                }
            }
            catch (Exception ex)
            {
                var newRetry = log.RetryCount + 1;
                var nextRetry = ComputeNextRetry(newRetry, now);

                if (newRetry >= 5)
                {
                    await compensationRepo.MoveToDeadLetterAsync(log.Id, ex.Message, now, ct);
                    result.DeadLetteredIds.Add(log.Id);
                }
                else
                {
                    result.Failed.Add((log.Id, ex.Message, newRetry, nextRetry));
                }
            }
        }

        if (result.ProcessedIds.Count > 0)
            await compensationRepo.MarkRangeAsProcessedAsync(result.ProcessedIds, now, ct);

        if (result.Failed.Count > 0)
            await compensationRepo.MarkRangeAsFailedAsync(result.Failed, ct);

        await uow.CommitWithoutEventsAsync(ct);

        return result;
    }

    /// <summary>Releases any locks this driver still holds (cleanup between tests).</summary>
    public async Task ReleaseLocksAsync(CancellationToken ct = default)
    {
        using var scope = _rootServices.CreateScope();
        var compensationRepo = scope.ServiceProvider.GetRequiredService<ICompensationLogRepository>();
        await compensationRepo.ReleaseLockAsync(_testLockId, ct);
    }

    private static async Task<Result> DispatchAsync(
        IPaymentService paymentService, CompensationLogDto log, CancellationToken ct)
    {
        if (log.CompensationType == "CancelPayment")
            return await paymentService.CancelPaymentAsync(log.BookingId, ct);

        return Result.Failure(Error.Failure(
            "Compensation.UnknownType",
            $"Unknown compensation type: {log.CompensationType}"));
    }

    private static readonly TimeSpan[] RetryBackoff =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
    ];

    private static DateTime? ComputeNextRetry(int retryCount, DateTime now)
    {
        if (retryCount <= 0) return null;
        var index = Math.Min(retryCount - 1, RetryBackoff.Length - 1);
        return now.Add(RetryBackoff[index]);
    }
}

/// <summary>Summary of a single compensation-processing cycle.</summary>
public sealed class CompensationCycleResult
{
    public List<Guid> ProcessedIds { get; } = new();
    public Dictionary<Guid, Guid> ProcessedByBooking { get; } = new();
    public List<Guid> DeadLetteredIds { get; } = new();
    public List<(Guid Id, string Error, int NewRetryCount, DateTime? NextRetryOnUtc)> Failed { get; } = new();
}
