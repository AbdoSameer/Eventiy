using Application.Abstractions.Messaging;

namespace Application.Features.Events.Commands.UpdatePhotoMetadata;

public sealed record UpdatePhotoMetadataCommand(
    Guid EventId,
    Guid PhotoId,
    string? Caption,
    int? DisplayOrder) : ICommand;
