using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
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

        public async Task<Result<EventDetailsResponse>> Handle(
     GetEventDetailsQuery request,
     CancellationToken cancellationToken)
        {
            var eventId = EventId.Create(request.Id);

            var result = await _context.Query<Event>()
                .Where(e => e.Id == eventId.Value)
                .Select(e => new EventDetailsResponse
                {
                    Id = e.Id.Value,
                    Date = e.Date,
                    Name = e.EventName.Value,
                    Description = e.Description,
                    Status = e.Status,

                    LowestTicketPrice = e.TicketTypes
                        .Min(t => (decimal?)t.Price.Amount) ?? 0m,

                    Location = new AddressResponse(
                        e.Location.Country,
                        e.Location.City,
                        e.Location.Street),

                    TicketDetails = e.TicketTypes
                        .Select(t => new TicketDetailsResponse(
                            t.Id.Value,
                            t.Price.Amount,
                            t.Price.Currency,
                            t.TicketTypeName,
                            t.Capacity))
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (result is null)
                return Result<EventDetailsResponse>.Failure(
                    Error.NotFound("Event.NotFound", $"Event with id {request.Id} was not found."));

            return Result<EventDetailsResponse>.Success(result);
        }
    }
}
