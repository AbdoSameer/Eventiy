using Domain.Common;

namespace Application.Abstractions.Outbox
{
    public interface IEventSerializer
    {
        string Serialize<T>(T @event) where T : IDomainEvent;
        IDomainEvent Deserialize(string eventName, string serializedEvent);
    }
}
