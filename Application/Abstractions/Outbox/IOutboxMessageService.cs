using Domain.Common;

namespace Application.Abstractions.Outbox
{
    public interface IOutboxMessageService
    {
        Task AddFromDomainEventsAsync(
            IEnumerable<IDomainEvent> domainEvents,
            CancellationToken cancellationToken = default);
    }
}