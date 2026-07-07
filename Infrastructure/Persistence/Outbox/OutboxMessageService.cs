using Application.Abstractions.Outbox;
using Domain.Common;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Persistence.Outbox;
public sealed class OutboxMessageService : IOutboxMessageService
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly IEventSerializer _serializer;
    private readonly ILogger<OutboxMessageService> _logger;

    public OutboxMessageService(
        IOutboxRepository outboxRepository,
        IEventSerializer serializer,
        ILogger<OutboxMessageService> logger)
    {
        _outboxRepository = outboxRepository;
        _serializer = serializer;
        _logger = logger;
    }

    public void AddFromDomainEvents(IEnumerable<IDomainEvent> domainEvents)
    {
        var events = domainEvents.ToList();
        if (events.Count == 0)
        {
            _logger.LogDebug("No domain events to stage in Outbox.");
            return;
        }

        var messages = events.Select(de =>
        {
            var payload = _serializer.Serialize(de);
            var idempotencyKey = ComputeIdempotencyKey(de, payload); 
            return new OutboxMessageDto(
                Id: Guid.NewGuid(),
                EventName: de.Name,
                Domain: de.Domain,
                Payload: payload,
                OccurredOnUtc: de.OccurredOnUtc,
                IdempotencyKey: idempotencyKey, 
                ProcessedOnUtc: null,
                NextRetryOnUtc: null,
                Error: null,
                RetryCount: 0);
        }).ToList();

        _outboxRepository.AddRange(messages);

        _logger.LogDebug(
            "Staged {Count} outbox messages: {Events}",
            messages.Count,
            string.Join(", ", events.Select(e => e.Name)));
    }

    private static string ComputeIdempotencyKey(IDomainEvent domainEvent, string payload)
    {
        if (domainEvent is DomainEvent de &&
            !string.IsNullOrWhiteSpace(de.Metadata?.CorrelationId))
        {
            var correlationKey = $"{domainEvent.Name}_{de.Metadata.CorrelationId}";
            return correlationKey.Length <= 100
                ? correlationKey
                : correlationKey[..100];
        }

        var raw = $"{domainEvent.Name}_{domainEvent.OccurredOnUtc:O}_{payload}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hashBytes)[..32]; 
    }
}
