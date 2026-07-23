using Application.Abstractions.Messaging;
using Application.Abstractions.Security;

namespace Application.Features.Authentication.Commands.ApproveOrganizer;

public sealed record ApproveOrganizerCommand(Guid UserId) : ICommand, IAuthorizableRequest
{
    public string[] RequiredRoles => ["Admin"];
}
