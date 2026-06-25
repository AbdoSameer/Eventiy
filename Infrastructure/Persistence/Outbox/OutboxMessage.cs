
using Domain.Common;

namespace Infrastructure.Persistence.Outbox
{
    public sealed class OutboxMessage
    {
        public Guid Id { get; private set; }
        public string EventName { get; private set; }     
        public string Domain { get; private set; }          
        public string Payload { get; private set; }
        public DateTime OccurredOnUtc { get; private set; } 
        public DateTime? ProcessedOnUtc { get; private set; }
        public string? Error { get; private set; }
        public int RetryCount { get; private set; }
        public DateTime? NextRetryOnUtc { get; private set; }

        private OutboxMessage() { } // EF Core

        public OutboxMessage(IDomainEvent domainEvent, string serializedPayload)
        {
            Id = Guid.NewGuid();
            EventName = domainEvent.Name;          
            Domain = domainEvent.Domain;           
            Payload = serializedPayload;
            OccurredOnUtc = domainEvent.OccurredOnUtc; 
            RetryCount = 0;
        }

        public bool IsProcessed => ProcessedOnUtc.HasValue;

        public bool IsReadyForProcessing =>
            !IsProcessed &&
            (!NextRetryOnUtc.HasValue || NextRetryOnUtc.Value <= DateTime.UtcNow);

        public void MarkAsProcessed()
            => ProcessedOnUtc = DateTime.UtcNow;

        public void MarkAsFailed(string error)
        {
            Error = error;
            RetryCount++;

            if (RetryCount < 3)
            {
                NextRetryOnUtc = DateTime.UtcNow.Add(GetRetryDelay(RetryCount));
            }
        }

        private static TimeSpan GetRetryDelay(int retryCount) => retryCount switch
        {
            1 => TimeSpan.FromSeconds(5),
            2 => TimeSpan.FromMinutes(1),
            _ => TimeSpan.FromMinutes(5)
        };
    }
}