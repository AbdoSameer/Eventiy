using Application.Abstractions.Messaging;


namespace Application.Features.Events.Commands.AddTicketType
{
    public sealed record AddTicketTypeCommand
        (
            Guid EventId,
            string Name,
            decimal Amount,
            string Currency,
            int Capacity
        ) :ICommand;
    
}
