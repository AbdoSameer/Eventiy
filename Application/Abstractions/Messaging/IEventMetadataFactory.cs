using Domain.Common;

namespace Application.Abstractions.Messaging;

public interface IEventMetadataFactory
{
    EventMetadata Create();
}
