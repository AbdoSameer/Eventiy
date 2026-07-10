namespace Application.Abstractions.Messaging;

public sealed record EventMetadata(
    string CorrelationId,
    string? CausationId,
    string? CreatedBy);

public interface IEventMetadataFactory
{
    EventMetadata Create();
}