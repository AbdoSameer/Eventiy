using Application.Abstractions.Outbox;
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

    // Dictionary: EventName → EventType
    private static readonly Dictionary<string, Type> _eventTypes;

    // Dictionary: EventName → Domain Name
    private static readonly Dictionary<string, string> _eventDomains;

    // Dictionary: EventType → EventName
    private static readonly Dictionary<Type, string> _eventNamesByType;

    static EventSerializer()
    {
        _eventTypes = new Dictionary<string, Type>();
        _eventDomains = new Dictionary<string, string>();
        _eventNamesByType = new Dictionary<Type, string>();

        var domainEventType = typeof(IDomainEvent);
        var allTypes = Assembly.GetAssembly(domainEventType)!
            .GetTypes()
            .Where(t => domainEventType.IsAssignableFrom(t) &&
                       t is { IsInterface: false, IsAbstract: false });

        foreach (var type in allTypes)
        {
            var eventName = type.Name;
            var domainName = ExtractDomainFromType(type);

            _eventTypes[eventName] = type;
            _eventDomains[eventName] = domainName;
            _eventNamesByType[type] = eventName;
        }
    }

    private static string ExtractDomainFromType(Type type)
    {
        var @namespace = type.Namespace ?? string.Empty;

        if (@namespace.Contains("BookingAggregate"))
            return "Booking";
        if (@namespace.Contains("EventAggregate"))
            return "Event";
        if (@namespace.Contains("UserAggregate"))
            return "User";
        if (@namespace.Contains("PaymentAggregate"))
            return "Payment";

        return "Unknown";
    }

    // Serialization

    public string Serialize<T>(T @event) where T : IDomainEvent
    {
        return JsonSerializer.Serialize(@event, _options);
    }

    // Deserialization

    public IDomainEvent Deserialize(string eventName, string serializedEvent)
    {
        if (!_eventTypes.TryGetValue(eventName, out var eventType))
        {
            throw new InvalidOperationException($"Unknown event type: {eventName}");
        }

        return (IDomainEvent)JsonSerializer.Deserialize(serializedEvent, eventType, _options)!;
    }

    public T Deserialize<T>(string serializedEvent) where T : IDomainEvent
    {
        return JsonSerializer.Deserialize<T>(serializedEvent, _options)!;
    }

    // Event Info (Implements Interface)

    /// <summary>
    /// ✅ الحصول على Domain Name من Event Name
    /// </summary>
    public string GetEventDomain(string eventName)
    {
        return _eventDomains.TryGetValue(eventName, out var domain)
            ? domain
            : "Unknown";
    }

    public Type GetEventType(string eventName)
    {
        return _eventTypes.TryGetValue(eventName, out var type)
            ? type
            : null!;
    }

    public string GetEventName(Type eventType)
    {
        return _eventNamesByType.TryGetValue(eventType, out var name)
            ? name
            : eventType.Name;
    }

    public bool IsEventRegistered(string eventName)
    {
        return _eventTypes.ContainsKey(eventName);
    }

    public IReadOnlyDictionary<string, Type> GetRegisteredEvents()
    {
        return _eventTypes.AsReadOnly();
    }

    public IReadOnlyDictionary<string, string> GetRegisteredDomains()
    {
        return _eventDomains.AsReadOnly();
    }

    public IEnumerable<string> GetEventsByDomain(string domain)
    {
        return _eventDomains
            .Where(x => x.Value == domain)
            .Select(x => x.Key);
    }

    public (string EventName, string Domain) GetEventInfo(Type eventType)
    {
        var eventName = GetEventName(eventType);
        var domain = GetEventDomain(eventName);
        return (eventName, domain);
    }
}