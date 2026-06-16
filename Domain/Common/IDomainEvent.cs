namespace Domain.Common
{
    public interface IDomainEvent
    {
        // this is a marker interface for domain events
        string Name { get; }
        string Domain { get; }

        public static string GetDomainEventName<T>() where T : IDomainEvent
        {
            return typeof(T).Name;
        }

    }
}
