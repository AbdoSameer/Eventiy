using Domain.Aggregates.EventAggregate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Abstractions.Persistence
{
    public interface IEventRepository
    {

        Task<Event> AddEventAsync(
            Event @event,
            CancellationToken cancellationToken);

    }
}
