using Application.Abstractions.Outbox;
using Domain.Common;
using Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Outbox;

public sealed class OutboxMessageService : IOutboxMessageService
{
    private readonly OutboxRepository _outboxRepository;
    private readonly IEventSerializer _serializer;
    private readonly ILogger<OutboxMessageService> _logger;

    public OutboxMessageService(
        OutboxRepository outboxRepository,
        IEventSerializer serializer,
        ILogger<OutboxMessageService> logger = null)
    {
        _outboxRepository = outboxRepository;
        _serializer = serializer;
        _logger = logger;
    }

    public void AddFromDomainEvents(IEnumerable<IDomainEvent> domainEvents)
    {
        var events = domainEvents.ToList();
        if (!events.Any())
        {
            _logger?.LogDebug("📭 No domain events to stage in Outbox");
            return;
        }

        // ✅ Build DTOs (not Infrastructure Entities)
        var messages = events
            .Select(de =>
            {
                var payload = _serializer.Serialize(de);
                return new OutboxMessageDto(
                    Id: Guid.NewGuid(),
                    EventName: de.Name,
                    Domain: de.Domain,
                    Payload: payload,
                    OccurredOnUtc: de.OccurredOnUtc,
                    ProcessedOnUtc: null,
                    NextRetryOnUtc: null,
                    Error: null,
                    RetryCount: 0,
                    IsProcessed: false,
                    IsReadyForProcessing: true);
            })
            .ToList();

        // ✅ Use Interface method
        _outboxRepository.AddRange(messages);

        _logger?.LogDebug(
            "📝 Staged {Count} outbox messages for atomic commit: {Events}",
            messages.Count,
            string.Join(", ", events.Select(e => e.Name)));
    }
}