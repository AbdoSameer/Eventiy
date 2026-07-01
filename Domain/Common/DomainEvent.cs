namespace Domain.Common
{
    public sealed class EventMetadata
    {
        public string? CorrelationId { get; }
        public string? CausationId { get; }
        public string? CreatedBy { get; }

        public int Version { get; }

        public static EventMetadata Empty => new(null, null, null, 1);

        public EventMetadata(string? correlationId, string? causationId, string? createdBy, int version = 1)
        {
            CorrelationId = correlationId;
            CausationId = causationId;
            CreatedBy = createdBy;
            Version = version;
        }
        public static EventMetadata Create(string? correlationId, string? causationId, string? createdBy, int version = 1)
        {
            return new EventMetadata(correlationId, causationId, createdBy, version);
        }
    }
    public abstract class DomainEvent : IDomainEvent
    {
        public  Guid Id { get; }
        public virtual string Name => GetType().Name;
        public virtual string Domain { get; }
        public DateTime OccurredOnUtc { get; }
        public string IdempotencyKey { get; } 
        public EventMetadata Metadata { get; }

        protected DomainEvent(string domain,
                              DateTime occurredOnUtc,
                              EventMetadata metadata)
        {
            Id = Guid.NewGuid();
            Domain = domain;
            OccurredOnUtc = occurredOnUtc;
            Metadata = metadata;
            IdempotencyKey = $"{metadata.CorrelationId}:{Name}:{Id}"; 
        }
        protected DomainEvent(DateTime occurredOnUtc,
                            EventMetadata metadata)
        {
            Id = Guid.NewGuid();
            OccurredOnUtc = occurredOnUtc;
            Metadata = metadata;
            IdempotencyKey = $"{metadata.CorrelationId}:{Name}:{Id}";
        }
    }
}