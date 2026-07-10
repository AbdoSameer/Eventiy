namespace Domain.Common
{
    public abstract class DomainEvent : IDomainEvent
    {
        public Guid Id { get; }
        public virtual string Name => GetType().Name;
        public virtual string Domain { get; }
        public DateTime OccurredOnUtc { get; }

        protected DomainEvent(string domain, DateTime occurredOnUtc)
        {
            Id = Guid.NewGuid();
            Domain = domain;
            OccurredOnUtc = occurredOnUtc;
        }
        protected DomainEvent(DateTime occurredOnUtc)
        {
            Id = Guid.NewGuid();
            OccurredOnUtc = occurredOnUtc;
            Domain = "Unknown";
        }
    }
}