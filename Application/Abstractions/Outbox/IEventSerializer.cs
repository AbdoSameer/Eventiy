using Domain.Common;

namespace Application.Abstractions.Outbox;

public interface IEventSerializer
{
    string Serialize<T>(T @event) where T : IDomainEvent;

    Result<IDomainEvent> Deserialize(string eventName, string serializedEvent);

   
    Result<T> Deserialize<T>(string serializedEvent) where T : IDomainEvent;


    string GetEventDomain(string eventName);
    Type GetEventType(string eventName);
    string GetEventName(Type eventType);
    bool IsEventRegistered(string eventName);


    IReadOnlyDictionary<string, Type> GetRegisteredEvents();
    IReadOnlyDictionary<string, string> GetRegisteredDomains();
    IEnumerable<string> GetEventsByDomain(string domain);
    (string EventName, string Domain) GetEventInfo(Type eventType);
}