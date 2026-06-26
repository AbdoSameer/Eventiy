using Domain.Common;

namespace Application.Abstractions.Outbox
{
    public interface IOutboxMessageService
    {
        void AddFromDomainEvents(IEnumerable<IDomainEvent> domainEvents);
    }
}