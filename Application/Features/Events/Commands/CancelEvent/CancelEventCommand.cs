using Application.Abstractions.Messaging;

namespace Application.Features.Events.Commands.CancelEvent;

public sealed record CancelEventCommand(Guid EventId) : ICommand;
