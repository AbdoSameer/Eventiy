namespace Domain.Common
{
    public interface IDomainEvent
    {
        string Name { get; }
        string Domain { get; }
        DateTime OccurredOnUtc { get; }
        string IdempotencyKey { get; }  
        EventMetadata Metadata { get; }
     

        public static string GetDomainEventName<T>() where T : IDomainEvent
        {
            return typeof(T).Name;
        }
    }
}