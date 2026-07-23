using Application.Abstractions.Messaging;
using Application.Abstractions.Security;

namespace Application.Features.Events.Commands.CancelEvent;

public sealed record CancelEventCommand(Guid EventId) : ICommand, IAuthorizableRequest
{
    public string[] RequiredRoles => ["Admin"];
}
