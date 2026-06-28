namespace Application.Abstractions.Outbox;

public interface IOutboxRepository
{
    void Add(OutboxMessageDto message);
    void AddRange(IEnumerable<OutboxMessageDto> messages);

    Task<IReadOnlyList<OutboxMessageDto>> GetAndLockUnprocessedMessagesAsync(
        Guid lockId,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task MarkRangeAsProcessedAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);

    Task MarkRangeAsFailedAsync(
        IEnumerable<OutboxFailedMessageUpdateDto> failedMessages,
        CancellationToken cancellationToken = default);

    Task ReleaseLockAsync(
        Guid lockId,
        CancellationToken cancellationToken = default);

    Task<int> GetUnprocessedCountAsync(
        CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record OutboxMessageDto(
    Guid Id,
    string EventName,
    string Domain,
    string Payload,
    DateTime OccurredOnUtc,
    string IdempotencyKey,
    DateTime? ProcessedOnUtc,
    DateTime? NextRetryOnUtc,
    string? Error,
    int RetryCount);

public sealed record OutboxFailedMessageUpdateDto(
    Guid Id,
    string Error,
    int NewRetryCount,
    DateTime? NextRetryOnUtc);