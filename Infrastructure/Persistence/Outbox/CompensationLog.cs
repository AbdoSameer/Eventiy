namespace Infrastructure.Persistence.Outbox;

public sealed class CompensationLog
{
    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public string CompensationType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime OccurredOnUtc { get; private set; }
    public DateTime? ProcessedOnUtc { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime? NextRetryOnUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid? ProcessingLock { get; private set; }
    public DateTime? ProcessingLockedAt { get; private set; }

    private const int LockTimeoutMinutes = 5;
    public const int MaxRetries = 5;

    private CompensationLog() { }

    public CompensationLog(
        Guid id,
        Guid bookingId,
        string compensationType,
        string payload,
        DateTime occurredOnUtc,
        string idempotencyKey)
    {
        Id = id;
        BookingId = bookingId;
        CompensationType = compensationType;
        Payload = payload;
        OccurredOnUtc = occurredOnUtc;
        IdempotencyKey = idempotencyKey;
    }

    public bool IsProcessed => ProcessedOnUtc.HasValue;

    public bool IsReadyForProcessing(DateTime currentTime) =>
        !IsProcessed &&
        (!NextRetryOnUtc.HasValue || NextRetryOnUtc.Value <= currentTime) &&
        !IsLocked(currentTime);

    public bool IsLocked(DateTime currentTime) =>
        ProcessingLock.HasValue &&
        ProcessingLockedAt.HasValue &&
        (currentTime - ProcessingLockedAt.Value).TotalMinutes < LockTimeoutMinutes;

    public bool TryAcquireLock(Guid lockId, DateTime currentTime)
    {
        if (IsLocked(currentTime) || IsProcessed) return false;

        ProcessingLock = lockId;
        ProcessingLockedAt = currentTime;
        return true;
    }

    public void ReleaseLock(Guid lockId)
    {
        if (ProcessingLock == lockId)
        {
            ProcessingLock = null;
            ProcessingLockedAt = null;
        }
    }

    public void MarkAsProcessed(DateTime currentTime)
    {
        ProcessedOnUtc = currentTime;
        ProcessingLock = null;
        ProcessingLockedAt = null;
    }

    public void MarkAsFailed(string error, DateTime currentTime, DateTime? nextRetryOnUtc = null)
    {
        Error = error;
        RetryCount++;
        ProcessingLock = null;
        ProcessingLockedAt = null;
        NextRetryOnUtc = nextRetryOnUtc;
    }

    public bool HasExhaustedRetries => RetryCount >= MaxRetries;
}
