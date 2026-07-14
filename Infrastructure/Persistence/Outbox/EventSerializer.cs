using Application.Abstractions.Outbox;
using Domain.Common;
using Infrastructure.Persistence.Outbox.Converters;
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
    private static readonly Dictionary<string, string> _eventDomains;
    private static readonly Dictionary<Type, string> _eventNamesByType;

    static EventSerializer()
    {
        _options.Converters.Add(new ValueObjectJsonConverterFactory());
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

    public string Serialize<T>(T @event) where T : IDomainEvent
    {
        return JsonSerializer.Serialize(@event, @event.GetType(), _options);
    }

    public Result<IDomainEvent> Deserialize(string eventName, string serializedEvent)
    {
        if (!_eventTypes.TryGetValue(eventName, out var eventType))
        {
            return Result<IDomainEvent>.Failure(
                Error.NotFound(
                    "Serializer.EventTypeNotFound",
                    $"Event type '{eventName}' is not registered. " +
                    $"Available: {string.Join(", ", _eventTypes.Keys.Take(10))}..."));
        }

        IDomainEvent? deserializedEvent;
        try
        {
            deserializedEvent = JsonSerializer.Deserialize(serializedEvent, eventType, _options) as IDomainEvent;
        }
        catch (JsonException ex)
        {
            return Result<IDomainEvent>.Failure(
                Error.Failure(
                    "Serializer.JsonDeserializationFailed",
                    $"Malformed JSON for event '{eventName}'. {ex.Message}"));
        }
        catch (NotSupportedException ex)
        {
            return Result<IDomainEvent>.Failure(
                Error.Failure(
                    "Serializer.TypeNotSupported",
                    $"Type '{eventType.Name}' not supported. {ex.Message}"));
        }

        if (deserializedEvent is null)
        {
            return Result<IDomainEvent>.Failure(
                Error.Failure(
                    "Serializer.DeserializationReturnedNull",
                    $"Payload for '{eventName}' deserialized to null. Payload may be empty."));
        }

        return Result<IDomainEvent>.Success(deserializedEvent);
    }

    public Result<T> Deserialize<T>(string serializedEvent) where T : IDomainEvent
    {
        var eventName = typeof(T).Name;
        var result = Deserialize(eventName, serializedEvent);

        if (result.IsFailure)
            return Result<T>.Failure(result.Errors.ToArray());

        if (result.Value is not T typedEvent)
        {
            return Result<T>.Failure(
                Error.Failure(
                    "Serializer.TypeMismatch",
                    $"Expected '{typeof(T).Name}', got '{result.Value.GetType().Name}'."));
        }

        return Result<T>.Success(typedEvent);
    }
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