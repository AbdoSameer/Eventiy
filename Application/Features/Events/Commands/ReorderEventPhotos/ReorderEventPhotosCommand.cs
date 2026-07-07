using Application.Abstractions.Messaging;

namespace Application.Features.Events.Commands.ReorderEventPhotos;

public sealed record ReorderEventPhotosCommand(
    Guid EventId,
    List<Guid> OrderedPhotoIds) : ICommand;
