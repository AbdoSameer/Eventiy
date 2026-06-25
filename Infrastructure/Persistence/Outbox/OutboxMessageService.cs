using Application.Abstractions.Outbox;
using Domain.Common; 

namespace Infrastructure.Persistence.Outbox; 
public sealed class OutboxMessageService : IOutboxMessageService
{
    private readonly ApplicationDbContext _context;
    private readonly IEventSerializer _serializer;

    public OutboxMessageService(ApplicationDbContext context, IEventSerializer serializer)
    {
        _context = context;
        _serializer = serializer;
    }

    public async Task AddFromDomainEventsAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        var events = domainEvents.ToList();
        if (!events.Any())
            return;

        foreach (var domainEvent in events)
        {
            // Serialize Domain Event
            var payload = _serializer.Serialize(domainEvent);

            // Create Infrastructure Entity
            var outboxMessage = new OutboxMessage(domainEvent, payload);

            // Save to Infrastructure DbContext
            await _context.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
        }
    }
}