using Domain.Common;

namespace Infrastructure.Persistence.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string EventName { get; private set; }
    public string Domain { get; private set; }
    public string Payload { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }
    public DateTime? ProcessedOnUtc { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime? NextRetryOnUtc { get; private set; }

    // ✅ NEW: Idempotency Key (Unique per event)
    public string IdempotencyKey { get; private set; }

    // ✅ NEW: Processing Lock
    public Guid? ProcessingLock { get; private set; }
    public DateTime? ProcessingLockedAt { get; private set; }
    private const int LockTimeoutMinutes = 5;

    private OutboxMessage() { }

    // ✅ Constructor for converting from DTO
    public OutboxMessage(
        Guid id,
        string eventName,
        string domain,
        string payload,
        DateTime occurredOnUtc,
        DateTime? processedOnUtc,
        DateTime? nextRetryOnUtc,
        string? error,
        int retryCount)
    {
        Id = id;
        EventName = eventName;
        Domain = domain;
        Payload = payload;
        OccurredOnUtc = occurredOnUtc;
        ProcessedOnUtc = processedOnUtc;
        NextRetryOnUtc = nextRetryOnUtc;
        Error = error;
        RetryCount = retryCount;
    }

    public bool IsProcessed => ProcessedOnUtc.HasValue;

    public bool IsReadyForProcessing =>
        !IsProcessed &&
        (!NextRetryOnUtc.HasValue || NextRetryOnUtc.Value <= DateTime.UtcNow) &&
        !IsLocked;

    public bool IsLocked =>
        ProcessingLock.HasValue &&
        ProcessingLockedAt.HasValue &&
        (DateTime.UtcNow - ProcessingLockedAt.Value).TotalMinutes < LockTimeoutMinutes;

    // ✅ Acquire Lock (Atomic Operation)
    public bool TryAcquireLock(Guid lockId)
    {
        if (IsLocked)
            return false;

        if (IsProcessed)
            return false;

        ProcessingLock = lockId;
        ProcessingLockedAt = DateTime.UtcNow;
        return true;
    }

    // ✅ Release Lock
    public void ReleaseLock(Guid lockId)
    {
        if (ProcessingLock == lockId)
        {
            ProcessingLock = null;
            ProcessingLockedAt = null;
        }
    }

    public void MarkAsProcessed()
    {
        ProcessedOnUtc = DateTime.UtcNow;
        ProcessingLock = null;
        ProcessingLockedAt = null;
    }

    public void MarkAsFailed(string error)
    {
        Error = error;
        RetryCount++;
        ProcessingLock = null;
        ProcessingLockedAt = null;

        if (RetryCount < 3)
        {
            NextRetryOnUtc = DateTime.UtcNow.Add(GetRetryDelay(RetryCount));
        }
    }

    // ✅ Unique Idempotency Key
    private static string GenerateIdempotencyKey(IDomainEvent domainEvent)
    {
        // استخدم Event Name + OccurredOnUtc + CorrelationId (إن وُجد) لضمان الـ Uniqueness
        var correlation = (domainEvent as DomainEvent)?.Metadata?.CorrelationId;
        if (!string.IsNullOrWhiteSpace(correlation))
        {
            return $"{domainEvent.Name}_{correlation}_{domainEvent.OccurredOnUtc:yyyyMMddHHmmssfff}";
        }

        return $"{domainEvent.Name}_{domainEvent.OccurredOnUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
    }

    private static TimeSpan GetRetryDelay(int retryCount) => retryCount switch
    {
        1 => TimeSpan.FromSeconds(5),
        2 => TimeSpan.FromMinutes(1),
        _ => TimeSpan.FromMinutes(5)
    };
}