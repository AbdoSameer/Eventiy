using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.Errors;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Common;
using Microsoft.EntityFrameworkCore;


namespace Application.Features.Bookings.Query.GetBookingDetails
{
    public class GetBookingDetailsQueryHandler
        : IQueryHandler<GetBookingDetailsQuery, GetBookingDetailsResponse>
    {
        private readonly IApplicationReadDbContext _context;

        public GetBookingDetailsQueryHandler(IApplicationReadDbContext context)
        {
            _context = context;
        }
        public async Task<Result<GetBookingDetailsResponse>> Handle(GetBookingDetailsQuery request, CancellationToken cancellationToken)
        {
            // Validate the Booking ID
            var bookingIdResult = BookingId.Create(request.BookingId);
            if (bookingIdResult.IsFailure)
            {
                return Result<GetBookingDetailsResponse>.Failure(
                    bookingIdResult.Error);
            }

            // Query the booking
            var booking = await _context.Query<Booking>()
                .Where(x => x.Id == bookingIdResult.Value)
                .Select(x => new GetBookingDetailsResponse(
                    x.Id.Value,
                    x.EventId.Value,
                    x.UserId.Value,
                    x.Quantity,
                    x.BookingDate,
                    x.Status.ToString(),
                    x.TotalAmount,
                    x.Money.Currency
                )).FirstOrDefaultAsync(cancellationToken);
                    
            // Check if booking exists
            if (booking is null)
            {
                return Result<GetBookingDetailsResponse>.Failure(
                    BookingErrors.BookingNotFound(request.BookingId));
            }

            return Result<GetBookingDetailsResponse>.Success(booking);

        }
    }
}
