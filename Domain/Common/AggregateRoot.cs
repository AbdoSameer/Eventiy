using Domain.Primitives;

namespace Domain.Common
{
    public abstract class AggregateRoot<TId> : Entity<TId> , IAggregateRoot
        where TId : ValueObjectBase
    {
        private readonly List<IDomainEvent> _domainEvents = new();
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        protected AggregateRoot(TId id) : base(id)
        {
        }

        protected AggregateRoot() { }

        protected void RaiseDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }
    }
}