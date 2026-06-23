namespace Domain.Common
{
    public abstract class DomainEvent : IDomainEvent
    {
        public DateTime OccurredOn { get; protected set; }
        public abstract string Name { get; }
        public abstract string Domain { get; }

        protected DomainEvent()
        {
            OccurredOn = DateTime.UtcNow;
        }
    }
}