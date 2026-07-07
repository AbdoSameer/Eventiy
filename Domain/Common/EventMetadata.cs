namespace Domain.Common
{
    public sealed class EventMetadata
    {
        public string? CorrelationId { get; }
        public string? CausationId { get; }
        public string? CreatedBy { get; }

        public int Version { get; }

        public static EventMetadata Empty => new(null, null, null, 1);

        public EventMetadata(string? correlationId, string? causationId, string? createdBy, int version = 1)
        {
            CorrelationId = correlationId;
            CausationId = causationId;
            CreatedBy = createdBy;
            Version = version;
        }
        public static EventMetadata Create(string? correlationId, string? causationId, string? createdBy, int version = 1)
        {
            return new EventMetadata(correlationId, causationId, createdBy, version);
        }
    }
}