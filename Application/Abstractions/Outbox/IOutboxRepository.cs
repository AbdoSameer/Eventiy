using Domain.Common;

namespace Application.Abstractions.Outbox;

/// <summary>
/// ✅ Repository Interface - Application Layer only knows this
/// </summary>
public interface IOutboxRepository
{
    void Add(OutboxMessageDto message);
    void AddRange(IEnumerable<OutboxMessageDto> messages);
    Task<IReadOnlyList<OutboxMessageDto>> GetAndLockUnprocessedMessagesAsync(
        Guid lockId,
        int batchSize,
        CancellationToken cancellationToken = default);
    Task MarkAsProcessedAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkAsFailedAsync(Guid id, string error, CancellationToken cancellationToken = default);
    void MarkRangeAsProcessed(IEnumerable<Guid> messageIds);
    void MarkRangeAsFailed(IEnumerable<(Guid Id, string Error)> failedMessages);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> GetUnprocessedCountAsync(CancellationToken cancellationToken = default);
    Task ReleaseLockAsync(Guid lockId, CancellationToken cancellationToken = default);
}

/// <summary>
/// ✅ DTO for Application Layer - No Infrastructure dependencies
/// </summary>
public sealed record OutboxMessageDto(
    Guid Id,
    string EventName,
    string Domain,
    string Payload,
    DateTime OccurredOnUtc,
    DateTime? ProcessedOnUtc,
    DateTime? NextRetryOnUtc,
    string? Error,
    int RetryCount,
    bool IsProcessed,
    bool IsReadyForProcessing);