using Application.Abstractions.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 50;
    private readonly Guid _lockId = Guid.NewGuid();

    public OutboxProcessor(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Processor started with LockId: {LockId}", _lockId);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Outbox Processor startup delay cancelled — shutting down");
            return;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingMessages(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing outbox messages");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        finally
        {
            await ReleaseAllLocksAsync();
            _logger.LogInformation("Outbox Processor stopped");
        }
    }

    private async Task ProcessPendingMessages(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var messages = await outboxRepository.GetAndLockUnprocessedMessagesAsync(
            _lockId, dateTimeProvider, BatchSize, cancellationToken);

        if (messages.Count == 0) return;

        await dispatcher.DispatchBatchAsync(messages, _lockId, dateTimeProvider, cancellationToken);
    }

    private async Task ReleaseAllLocksAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            await outboxRepository.ReleaseLockAsync(_lockId);
            _logger.LogInformation("Released all locks for LockId: {LockId}", _lockId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release locks");
        }
    }
}
