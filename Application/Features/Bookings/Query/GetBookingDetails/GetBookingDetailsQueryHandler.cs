using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;

namespace Application.Features.Bookings.Query.GetBookingDetails;

public class GetBookingDetailsQueryHandler
    : IQueryHandler<GetBookingDetailsQuery, GetBookingDetailsResponse>
{
    private readonly IApplicationReadDbContext _context;

    public GetBookingDetailsQueryHandler(IApplicationReadDbContext context)
    {
        _context = context;
    }

    public async Task<Result<GetBookingDetailsResponse>> Handle(
        GetBookingDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var bookingIdResult = BookingId.Create(request.BookingId);
        if (bookingIdResult.IsFailure)
            return Result<GetBookingDetailsResponse>.Failure(
                bookingIdResult.Errors.ToArray());

        var query = _context.Query<Booking>()
            .Where(x => x.Id == bookingIdResult.Value)
            .Select(x => new GetBookingDetailsResponse(
                x.Id.Value,
                x.EventId.Value,
                x.UserId.Value,
                x.TicketTypeId.Value,
                x.EventTitle,
                x.Quantity,
                x.BookingDate,
                x.Status.ToString(),
                x.TotalAmount,
                x.Money.Currency
            ));

        var booking = await _context.FirstOrDefaultAsync(query, cancellationToken);

        if (booking is null)
            return Result<GetBookingDetailsResponse>.Failure(
                BookingErrors.BookingNotFound(request.BookingId));

        return Result<GetBookingDetailsResponse>.Success(booking);
    }
}