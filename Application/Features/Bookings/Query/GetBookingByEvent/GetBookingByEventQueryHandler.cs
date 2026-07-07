using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Application.Features.Bookings.Query.GetBookingByEvent;

public class GetBookingByEventQueryHandler
    : IQueryHandler<GetBookingByEventQuery, List<GetBookingByEventQueryResponse>>
{
    private readonly IApplicationReadDbContext _context;

    public GetBookingByEventQueryHandler(IApplicationReadDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<GetBookingByEventQueryResponse>>> Handle(
        GetBookingByEventQuery request,
        CancellationToken cancellationToken)
    {
        var eventIdResult = EventId.Create(request.EventId);
        if (eventIdResult.IsFailure)
            return Result<List<GetBookingByEventQueryResponse>>
                .Failure(eventIdResult.Errors.ToArray());

        var query = _context.Query<Booking>()
            .Where(b => b.EventId == eventIdResult.Value)
            .Select(b => new GetBookingByEventQueryResponse(
                b.Id.Value,
                b.EventId.Value,
                b.UserId.Value,
                b.BookingDate,
                b.Quantity,
                b.TotalAmount
            ));

        var bookings = await _context.ToListAsync(query, cancellationToken);

        return Result<List<GetBookingByEventQueryResponse>>.Success(bookings);
    }
}