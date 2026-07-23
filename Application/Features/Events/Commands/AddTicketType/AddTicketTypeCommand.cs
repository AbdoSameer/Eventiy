using Application.Abstractions.Messaging;
using Application.Abstractions.Security;

namespace Application.Features.Events.Commands.AddTicketType
{
    public sealed record AddTicketTypeCommand
        (
            Guid EventId,
            string Name,
            decimal Amount,
            string Currency,
            int Capacity,
            string? SectionCode = null
        ) : ICommand, IAuthorizableRequest
    {
        public string[] RequiredRoles => ["Admin", "Organizer"];
    }

}
