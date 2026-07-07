namespace Infrastructure.Persistence;

public sealed class ProcessedEvent
{
    public Guid EventId { get; init; }
    public string IdempotencyKey { get; init; } = string.Empty;
    public DateTime ProcessedAt { get; init; }
}
