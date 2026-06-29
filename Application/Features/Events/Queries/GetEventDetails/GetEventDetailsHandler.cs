using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;

namespace Application.Features.Events.Queries.GetEventDetails;

public class GetEventDetailsHandler
    : IQueryHandler<GetEventDetailsQuery, EventDetailsResponse>
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
        var eventIdResult = EventId.Create(request.Id);
        if (eventIdResult.IsFailure)
            return Result<EventDetailsResponse>.Failure(
                eventIdResult.Errors.ToArray());

        var query = _context.Query<Event>()
            .Where(e => e.Id == eventIdResult.Value)
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
            });

        var result = await _context.FirstOrDefaultAsync(query, cancellationToken);

        if (result is null)
            return Result<EventDetailsResponse>.Failure(
                EventErrors.NotFound(request.Id));

        return Result<EventDetailsResponse>.Success(result);
    }
}