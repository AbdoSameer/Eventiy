using Application.Abstractions.Outbox;
using Infrastructure.Persistence.Repositories;
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

    // Infrastructure/BackgroundJobs/OutboxProcessor.cs
    private async Task ProcessPendingMessages(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<OutboxRepository>();
        var serializer = scope.ServiceProvider.GetRequiredService<IEventSerializer>();

        var messages = await outboxRepository.GetAndLockUnprocessedMessagesAsync(
            _lockId, BatchSize, cancellationToken);

        if (messages.Count == 0) return;

        var processedIds = new List<Guid>();
        var failedMessages = new List<(Guid Id, string Error)>();

        foreach (var message in messages)
        {
            try
            {
                var domainEvent = serializer.Deserialize(message.EventName, message.Payload);
                var domain = serializer.GetEventDomain(message.EventName);

                await PublishToMessageBrokerAsync(domain, domainEvent, cancellationToken);

                // ✅ تجميع بدلاً من SaveChanges لكل message
                processedIds.Add(message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to process message {MessageId}", message.Id);
                failedMessages.Add((message.Id, ex.Message));
            }
        }

        // ✅ Batch update — SaveChanges واحد للكل
        outboxRepository.MarkRangeAsProcessed(processedIds);
        outboxRepository.MarkRangeAsFailed(failedMessages);
        await outboxRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task PublishToMessageBrokerAsync(string domain, object domainEvent, CancellationToken cancellationToken)
    {
        // 🟢 Publish to RabbitMQ / Azure Service Bus / Kafka
        // Include IdempotencyKey in the message
        await Task.CompletedTask;
    }

    private async Task ReleaseAllLocksAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var outboxRepository = scope.ServiceProvider.GetRequiredService<OutboxRepository>();
            await outboxRepository.ReleaseLockAsync(_lockId);
            _logger.LogInformation("Released all locks for LockId: {LockId}", _lockId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release locks");
        }
    }
}