using Application.Abstractions.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MediatR;

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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessages(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        await ReleaseAllLocksAsync();
        _logger.LogInformation("Outbox Processor stopped");
    }

    private async Task ProcessPendingMessages(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var serializer = scope.ServiceProvider.GetRequiredService<IEventSerializer>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var messages = await outboxRepository.GetAndLockUnprocessedMessagesAsync(
            _lockId, BatchSize, cancellationToken);

        if (messages.Count == 0) return;

        var processedIds = new List<Guid>();
        var failedMessages = new List<OutboxFailedMessageUpdateDto>();

        foreach (var message in messages)
        {
            try
            {
                var domainEvent = serializer.Deserialize(message.EventName, message.Payload);

                await publisher.Publish(domainEvent, cancellationToken);

                processedIds.Add(message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to process message {MessageId}", message.Id);

                var newRetryCount = message.RetryCount + 1;
                var nextRetryOnUtc = GetNextRetryOnUtc(newRetryCount);

                failedMessages.Add(new OutboxFailedMessageUpdateDto(
                    Id: message.Id,
                    Error: ex.Message,
                    NewRetryCount: newRetryCount,
                    NextRetryOnUtc: nextRetryOnUtc
                ));
            }
        }

        if (processedIds.Any())
        {
            await outboxRepository.MarkRangeAsProcessedAsync(processedIds, cancellationToken);
        }

        if (failedMessages.Any())
        {
            await outboxRepository.MarkRangeAsFailedAsync(failedMessages, cancellationToken);
        }

        await outboxRepository.SaveChangesAsync(cancellationToken);
    }

    private static DateTime? GetNextRetryOnUtc(int retryCount)
    {
        if (retryCount >= 3)
            return null;

        var delay = retryCount switch
        {
            1 => TimeSpan.FromSeconds(5),
            2 => TimeSpan.FromMinutes(1),
            _ => TimeSpan.FromMinutes(5)
        };

        return DateTime.UtcNow.Add(delay);
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