namespace Infrastructure.Persistence.Outbox;

public sealed class OutboxDeadLetter
{
    public Guid Id { get; init; }
    public string EventName { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public DateTime OccurredOnUtc { get; init; }
    public string IdempotencyKey { get; init; } = string.Empty;
    public int RetryCount { get; init; }
    public string FailedReason { get; init; } = string.Empty;
    public DateTime MovedToDeadLetterAt { get; init; }
}
