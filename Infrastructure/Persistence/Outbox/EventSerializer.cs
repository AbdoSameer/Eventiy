using Application.Abstractions.Outbox;
using Domain.Aggregates.BookingAggregate.Events;
using Domain.Aggregates.EventAggregate.Events;
using Domain.Common;
using System.Reflection;
using System.Text.Json;


namespace Infrastructure.Persistence.Outbox;
public sealed class EventSerializer : IEventSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly Dictionary<string, Type> _eventTypes;

    static EventSerializer()
    {
        _eventTypes = new Dictionary<string, Type>();

        var domainEventType = typeof(IDomainEvent);
        var types = Assembly.GetAssembly(domainEventType)!
            .GetTypes()
            .Where(t => domainEventType.IsAssignableFrom(t) &&
                       t is { IsInterface: false, IsAbstract: false });

        foreach (var type in types)
        {
            // Use your existing Name property for lookup
            var instance = (IDomainEvent?)Activator.CreateInstance(type, true);
            if (instance != null)
            {
                _eventTypes[instance.Name] = type;
            }
        }
    }

    public string Serialize<T>(T @event) where T : IDomainEvent
    {
        return JsonSerializer.Serialize(@event, _options);
    }

    public IDomainEvent Deserialize(string eventName, string serializedEvent)
    {
        if (!_eventTypes.TryGetValue(eventName, out var eventType))
        {
            throw new InvalidOperationException($"Unknown event type: {eventName}");
        }

        return (IDomainEvent)JsonSerializer.Deserialize(serializedEvent, eventType, _options)!;
    }

    public string GetEventDomain(string eventName)
    {
        // You can also store Domain in the outbox message directly
        // Or use a separate mapping
        return eventName switch
        {
            nameof(BookingCreatedEvent) => "Booking",
            nameof(BookingConfirmedEvent) => "Booking",
            nameof(EventCreatedEvent) => "Event",
            _ => "Unknown"
        };
    }
}