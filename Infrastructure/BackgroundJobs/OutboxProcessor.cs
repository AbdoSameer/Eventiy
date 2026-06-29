using Application.Abstractions.Outbox;
using Application.Abstractions.Persistence;
using Domain.Common;
using Infrastructure.Persistence;
using MediatR;
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
        var serializer = scope.ServiceProvider.GetRequiredService<IEventSerializer>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = dateTimeProvider.UtcNow;

        var messages = await outboxRepository.GetAndLockUnprocessedMessagesAsync(
            _lockId, dateTimeProvider, BatchSize, cancellationToken);

        if (messages.Count == 0) return;

        var processedIds = new List<Guid>();
        var failedMessages = new List<OutboxFailedMessageUpdateDto>();

        foreach (var messageDto in messages)
        {
            var processResult = await ProcessSingleMessageAsync(
                messageDto, serializer, publisher, dateTimeProvider, cancellationToken);

            if (processResult.IsSuccess)
            {
                processedIds.Add(messageDto.Id);
                continue;
            }

            var isRetryable = processResult.Errors.All(e =>
                e.Code is not "Serializer.EventTypeNotFound" and
                         not "Serializer.JsonDeserializationFailed" and
                         not "Serializer.DeserializationReturnedNull");

            var newRetryCount = messageDto.RetryCount + 1;
            var nextRetryOnUtc = isRetryable && newRetryCount < 3
                ? GetNextRetryOnUtc(newRetryCount, dateTimeProvider)
                : (DateTime?)null;

            failedMessages.Add(new OutboxFailedMessageUpdateDto(
                Id: messageDto.Id,
                Error: string.Join(" | ", processResult.Errors.Select(e => $"{e.Code}: {e.Message}")),
                NewRetryCount: newRetryCount,
                NextRetryOnUtc: nextRetryOnUtc));
        }

        // ✅ explicit transaction — الـ status updates atomically
        await using var tx = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (processedIds.Any())
                await outboxRepository.MarkRangeAsProcessedAsync(processedIds, now, cancellationToken);

            if (failedMessages.Any())
                await outboxRepository.MarkRangeAsFailedAsync(failedMessages, cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit outbox status updates — rolling back");
            await tx.RollbackAsync(cancellationToken);
        }
    }
    private async Task<Result> ProcessSingleMessageAsync(
        OutboxMessageDto messageDto,
        IEventSerializer serializer,
        IPublisher publisher,
        IDateTimeProvider dateTimeProvider,
        CancellationToken cancellationToken)
    {
        var deserializeResult = serializer.Deserialize(messageDto.EventName, messageDto.Payload);

        if (deserializeResult.IsFailure)
        {
            _logger.LogWarning(
                "Deserialization failed for message {MessageId}: {Errors}",
                messageDto.Id,
                string.Join("; ", deserializeResult.Errors.Select(e => e.Code)));

            return Result.Failure(deserializeResult.Errors.ToArray());
        }

        var domainEvent = deserializeResult.Value;

        try
        {
            await publisher.Publish(domainEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Publisher failed for message {MessageId}", messageDto.Id);

            return Result.Failure(
                Error.Failure(
                    "Outbox.PublishFailed",
                    $"Event publisher failed: {ex.Message}"));
        }

        return Result.Success();
    }

    private static DateTime? GetNextRetryOnUtc(int retryCount, IDateTimeProvider dateTimeProvider)
    {
        if (retryCount >= 3)
            return null;

        var delay = retryCount switch
        {
            1 => TimeSpan.FromSeconds(5),
            2 => TimeSpan.FromMinutes(1),
            _ => TimeSpan.FromMinutes(5)
        };

        return dateTimeProvider.UtcNow.Add(delay);
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