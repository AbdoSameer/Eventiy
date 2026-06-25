namespace Domain.Common
{
    public abstract class DomainEvent : IDomainEvent
    {
        public DateTime OccurredOnUtc { get;  }
        public abstract string Name { get; }
        public abstract string Domain { get; }

        protected DomainEvent()
        {
            OccurredOnUtc = DateTime.UtcNow;
        }
    }
}