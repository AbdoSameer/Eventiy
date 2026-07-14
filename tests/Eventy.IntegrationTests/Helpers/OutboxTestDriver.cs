using Application.Abstractions.Outbox;

namespace Eventy.IntegrationTests.Helpers;

public sealed class OutboxTestDriver
{
    private readonly IServiceProvider _rootServices;
    private readonly Guid _testLockId = Guid.NewGuid();

    public OutboxTestDriver(IServiceProvider rootServices) => _rootServices = rootServices;

    public async Task<int> ProcessOnceAsync(CancellationToken ct = default)
    {
        using var scope = _rootServices.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var messages = await outboxRepo.GetAndLockUnprocessedMessagesAsync(
            _testLockId, timeProvider, batchSize: 50, cancellationToken: ct);

        if (messages.Count == 0)
            return 0;

        var result = await dispatcher.DispatchBatchAsync(messages, _testLockId, timeProvider, ct);

        return result.ProcessedIds.Count;
    }
}
