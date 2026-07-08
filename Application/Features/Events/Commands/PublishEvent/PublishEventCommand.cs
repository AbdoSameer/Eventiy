using Application.Abstractions.Messaging;

namespace Application.Features.Events.Commands.PublishEvent;

public sealed record PublishEventCommand(Guid EventId) : ICommand;
