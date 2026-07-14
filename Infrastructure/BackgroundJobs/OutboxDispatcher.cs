using Application.Abstractions.Outbox;
using Application.Abstractions.Persistence;
using Domain.Common;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

public sealed class OutboxDispatcher : IOutboxDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IServiceProvider serviceProvider,
        ILogger<OutboxDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<OutboxDispatchResult> DispatchBatchAsync(
        IReadOnlyList<OutboxMessageDto> messages,
        Guid lockId,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
            return new OutboxDispatchResult(true, Array.Empty<Guid>(), Array.Empty<OutboxFailedMessageUpdateDto>());

        using var scope = _serviceProvider.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var serializer = scope.ServiceProvider.GetRequiredService<IEventSerializer>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var processedIds = new List<Guid>();
        var failedMessages = new List<OutboxFailedMessageUpdateDto>();

        foreach (var messageDto in messages)
        {
            var processResult = await DispatchSingleAsync(
                messageDto, serializer, scope.ServiceProvider, cancellationToken);

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
            var error = string.Join(" | ", processResult.Errors.Select(e => $"{e.Code}: {e.Message}"));

            if (!isRetryable || newRetryCount >= 3)
            {
                await outboxRepository.MoveToDeadLetterAsync(
                    messageDto.Id, error, utcNow, cancellationToken);
                continue;
            }

            var nextRetryOnUtc = GetNextRetryOnUtc(newRetryCount, timeProvider);
            failedMessages.Add(new OutboxFailedMessageUpdateDto(
                Id: messageDto.Id,
                Error: error,
                NewRetryCount: newRetryCount,
                NextRetryOnUtc: nextRetryOnUtc));
        }

        await context.SaveChangesAsync(cancellationToken);

        if (processedIds.Count > 0)
            await outboxRepository.MarkRangeAsProcessedAsync(processedIds, utcNow, cancellationToken);

        if (failedMessages.Count > 0)
            await outboxRepository.MarkRangeAsFailedAsync(failedMessages, cancellationToken);

        return new OutboxDispatchResult(true, processedIds, failedMessages);
    }

    private async Task<Result> DispatchSingleAsync(
        OutboxMessageDto messageDto,
        IEventSerializer serializer,
        IServiceProvider serviceProvider,
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
        var eventType = domainEvent.GetType();

        try
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
            var handler = serviceProvider.GetService(handlerType);

            if (handler is null)
            {
                _logger.LogDebug("No handler registered for {EventType}", messageDto.EventName);
                return Result.Success();
            }

            var handleMethod = handlerType.GetMethod("HandleAsync")!;
            var task = (Task<Result>)handleMethod.Invoke(handler, [domainEvent, cancellationToken])!;

            return await task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handler failed for message {MessageId}", messageDto.Id);

            return Result.Failure(
                Error.Failure(
                    "Outbox.HandlerFailed",
                    $"Event handler failed: {ex.Message}"));
        }
    }

    private static DateTime? GetNextRetryOnUtc(int retryCount, TimeProvider dateTimeProvider)
    {
        if (retryCount >= 3)
            return null;

        var delay = retryCount switch
        {
            1 => TimeSpan.FromSeconds(5),
            2 => TimeSpan.FromMinutes(1),
            _ => TimeSpan.FromMinutes(5)
        };

        return dateTimeProvider.GetUtcNow().UtcDateTime.Add(delay);
    }
}
