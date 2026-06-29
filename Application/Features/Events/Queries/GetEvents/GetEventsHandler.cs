using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate;
using Domain.Common;

namespace Application.Features.Events.Queries.GetEvents;

public class GetEventsHandler
    : IQueryHandler<GetEventsQuery, List<EventCardResponse>>
{
    private readonly IApplicationReadDbContext _context;

    public GetEventsHandler(IApplicationReadDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<EventCardResponse>>> Handle(
        GetEventsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Query<Event>()
            .Where(e => e.Date > DateTime.UtcNow)
            .Select(e => new EventCardResponse(
                e.Id.Value,
                e.EventName.Value,
                e.Date
            ));

        var events = await _context.ToListAsync(query, cancellationToken);

        return Result<List<EventCardResponse>>.Success(events);
    }
}