using Domain.Common;

namespace Application.Abstractions.Outbox;

public interface IEventSerializer
{
    // ✅ Serialization
    string Serialize<T>(T @event) where T : IDomainEvent;

    // ✅ Deserialization
    IDomainEvent Deserialize(string eventName, string serializedEvent);
    T Deserialize<T>(string serializedEvent) where T : IDomainEvent;

    // ✅ Event Info (بدون إنشاء Instance)
    string GetEventDomain(string eventName);
    Type GetEventType(string eventName);
    string GetEventName(Type eventType);
    bool IsEventRegistered(string eventName);

    // ✅ Bulk Operations
    IReadOnlyDictionary<string, Type> GetRegisteredEvents();
    IReadOnlyDictionary<string, string> GetRegisteredDomains();
    IEnumerable<string> GetEventsByDomain(string domain);
    (string EventName, string Domain) GetEventInfo(Type eventType);
}