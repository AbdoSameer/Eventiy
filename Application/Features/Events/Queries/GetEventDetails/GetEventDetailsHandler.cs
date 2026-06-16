using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate;
using Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Events.Queries.GetEventDetails
{
    public class GetEventDetailsHandler :
        IQueryHandler<GetEventDetailsQuery, EventDetailsResponse>
    {
        private readonly IApplicationReadDbContext _context;

        public GetEventDetailsHandler(IApplicationReadDbContext context)
        {
            _context = context;
        }

        public async Task<Result<EventDetailsResponse>>
            Handle(
            GetEventDetailsQuery request,
            CancellationToken cancellationToken)
        {
            var result = await _context.Query<Event>()
                .Where(e => e.Id.Value == request.EventId)
                .Select(e => new EventDetailsResponse
                {
                    Id = e.Id.Value,
                    Date = e.Date,
                    Name = e.Name.Value,
                    Description = e.Description,
                    Status = e.Status,
                    LowestTicketPrice = e.TicketTypes.
                    Min(t => t.Price.Amount),

                    Location = new AddressResponse
                    {
                        Street = e.Location.Street,
                        City = e.Location.City,
                        Country = e.Location.Country
                    },

                    TicketDetails = e.TicketTypes.Select(t => new TicketDetailsResponse
                    {
                        Id = t.Id.Value,
                        Price = t.Price.Amount,
                        Currency = t.Price.Currency,
                    }).ToList()

                }).FirstOrDefaultAsync(cancellationToken);

         
            return Result<EventDetailsResponse>.Success(result!);
        }
    }
}
