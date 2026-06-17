using Application.Abstractions.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Events.Commands.AddTicketType
{
    public sealed record AddTicketTypeCommand
        (
            Guid EventId,
            string Name,
            decimal Amount,
            string Currency,
            int capacity
        ) :ICommand;
    
}
