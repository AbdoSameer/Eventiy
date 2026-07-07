using Application.Abstractions.Messaging;

namespace Application.Features.Events.Commands.SetCoverPhoto;

public sealed record SetCoverPhotoCommand(Guid EventId, Guid PhotoId) : ICommand;
