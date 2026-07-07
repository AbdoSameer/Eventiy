
namespace Domain.Common
{
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