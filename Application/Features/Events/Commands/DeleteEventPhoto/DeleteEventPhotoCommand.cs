using Application.Abstractions.Messaging;

namespace Application.Features.Events.Commands.DeleteEventPhoto;

public sealed record DeleteEventPhotoCommand(Guid EventId, Guid PhotoId) : ICommand;
