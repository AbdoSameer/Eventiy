using Domain.Aggregates.EventAggregate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Persistence
{
    public interface IAddTicketTypeRepository
    {
        Task<TicketType> AddTicketTypeAsync(
            TicketType ticketType,
            CancellationToken cancellationToken);
    }
}
