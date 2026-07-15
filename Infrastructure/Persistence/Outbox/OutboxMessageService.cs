using Application.Abstractions;
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
    private readonly IEventMetadataFactory _metadataFactory;
    private readonly ILogger<OutboxMessageService> _logger;

    public OutboxMessageService(
        IOutboxRepository outboxRepository,
        IEventSerializer serializer,
        IEventMetadataFactory metadataFactory,
        ILogger<OutboxMessageService> logger)
    {
        _outboxRepository = outboxRepository;
        _serializer = serializer;
        _metadataFactory = metadataFactory;
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

        var metadata = _metadataFactory.Create();

        var messages = events.Select(de =>
        {
            var id = Guid.NewGuid();
            var payload = _serializer.Serialize(de);
            var idempotencyKey = ComputeIdempotencyKey(id, de, metadata, payload);
            return new OutboxMessageDto(
                Id: id,
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

    private static string ComputeIdempotencyKey(Guid messageId, IDomainEvent domainEvent, EventMetadata metadata, string payload)
    {
        if (!string.IsNullOrWhiteSpace(metadata.CorrelationId))
        {
            var correlationKey = $"{domainEvent.Name}_{metadata.CorrelationId}_{messageId:N}";
            return correlationKey.Length <= 100
                ? correlationKey
                : correlationKey[..100];
        }

        var raw = $"{domainEvent.Name}_{domainEvent.OccurredOnUtc:O}_{messageId:N}_{payload}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hashBytes)[..32];
    }
}