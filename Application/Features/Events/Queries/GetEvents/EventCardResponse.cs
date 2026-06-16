using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Events.Queries.GetEvents
{
    public sealed record EventCardResponse(
        Guid Id,
        string Title,
        DateTime Date);
}
