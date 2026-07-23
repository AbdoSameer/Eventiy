using Application.Abstractions.Payments;
using Application.Abstractions.Persistence;
using Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

public sealed class CompensationProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CompensationProcessor> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 50;
    private readonly Guid _lockId = Guid.NewGuid();

    private const int MaxRetries = 5;

    private static readonly TimeSpan[] RetryBackoff =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
    ];

    public CompensationProcessor(
        IServiceProvider serviceProvider,
        ILogger<CompensationProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Compensation Processor started with LockId: {LockId}", _lockId);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Compensation Processor startup delay cancelled");
            return;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingCompensations(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing compensation logs");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        finally
        {
            await ReleaseAllLocksAsync();
            _logger.LogInformation("Compensation Processor stopped");
        }
    }

    private async Task ProcessPendingCompensations(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var compensationRepo = scope.ServiceProvider.GetRequiredService<ICompensationLogRepository>();
        var executionService = scope.ServiceProvider.GetRequiredService<ICompensationExecutionService>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var now = timeProvider.GetUtcNow().UtcDateTime;

        var pendingLogs = await compensationRepo.GetAndLockUnprocessedAsync(
            _lockId, timeProvider, BatchSize, ct);

        if (pendingLogs.Count == 0) return;

        _logger.LogInformation(
            "Processing {Count} pending compensation log(s)", pendingLogs.Count);

        var processedIds = new List<Guid>();
        var failedLogs = new List<(Guid Id, string Error, int NewRetryCount, DateTime? NextRetryOnUtc)>();

        foreach (var log in pendingLogs)
        {
            try
            {
                var result = await executionService.ExecuteAsync(log, ct);

                if (result.IsSuccess)
                {
                    processedIds.Add(log.Id);
                    _logger.LogInformation(
                        "Compensation {CompensationId} for booking {BookingId} processed successfully",
                        log.Id, log.BookingId);
                }
                else
                {
                    var newRetryCount = log.RetryCount + 1;
                    var nextRetry = ComputeNextRetry(newRetryCount, now);
                    var errorMsg = result.Errors.Select(e => e.Message)
                        .Aggregate((a, b) => $"{a}; {b}");

                    if (newRetryCount >= MaxRetries)
                    {
                        _logger.LogWarning(
                            "Compensation {CompensationId} for booking {BookingId} exhausted {MaxRetries} retries — moving to dead-letter",
                            log.Id, log.BookingId, MaxRetries);

                        await compensationRepo.MoveToDeadLetterAsync(
                            log.Id, errorMsg, now, ct);

                        processedIds.Add(log.Id);
                    }
                    else
                    {
                        failedLogs.Add((log.Id, errorMsg, newRetryCount, nextRetry));

                        _logger.LogWarning(
                            "Compensation {CompensationId} for booking {BookingId} failed (attempt {Retry}): {Error}",
                            log.Id, log.BookingId, newRetryCount, errorMsg);
                    }
                }
            }
            catch (Exception ex)
            {
                var newRetryCount = log.RetryCount + 1;
                var nextRetry = ComputeNextRetry(newRetryCount, now);

                if (newRetryCount >= MaxRetries)
                {
                    _logger.LogError(ex,
                        "Compensation {CompensationId} for booking {BookingId} threw exception and exhausted retries — moving to dead-letter",
                        log.Id, log.BookingId);

                    await compensationRepo.MoveToDeadLetterAsync(
                        log.Id, ex.Message, now, ct);

                    processedIds.Add(log.Id);
                }
                else
                {
                    failedLogs.Add((log.Id, ex.Message, newRetryCount, nextRetry));

                    _logger.LogError(ex,
                        "Compensation {CompensationId} for booking {BookingId} threw exception (attempt {Retry})",
                        log.Id, log.BookingId, newRetryCount);
                }
            }
        }

        if (processedIds.Count > 0)
            await compensationRepo.MarkRangeAsProcessedAsync(processedIds, now, ct);

        if (failedLogs.Count > 0)
            await compensationRepo.MarkRangeAsFailedAsync(failedLogs, ct);

        await uow.CommitWithoutEventsAsync(ct);
    }

    private static DateTime? ComputeNextRetry(int retryCount, DateTime now)
    {
        if (retryCount <= 0) return null;
        var index = Math.Min(retryCount - 1, RetryBackoff.Length - 1);
        return now.Add(RetryBackoff[index]);
    }

    private async Task ReleaseAllLocksAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var compensationRepo = scope.ServiceProvider.GetRequiredService<ICompensationLogRepository>();
            await compensationRepo.ReleaseLockAsync(_lockId);
            _logger.LogInformation("Released all compensation locks for LockId: {LockId}", _lockId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release compensation locks");
        }
    }
}
