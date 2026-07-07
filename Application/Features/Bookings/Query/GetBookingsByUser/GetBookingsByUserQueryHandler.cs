using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.EventAggregate;
using Domain.Common;


namespace Application.Features.Bookings.Query.GetBookingsByUser
{

    internal sealed class GetBookingsByUserQueryHandler(
        IApplicationReadDbContext _context,
        ICurrentUserService currentUser) : IQueryHandler<GetBookingsByUserQuery, List<BookingByUserResponse>>
    {
        public async Task<Result<List<BookingByUserResponse>>> Handle(
            GetBookingsByUserQuery query, CancellationToken ct)
        {
            var userIdResult = currentUser.GetCurrentUserId();
            if (userIdResult.IsFailure)
                return Result<List<BookingByUserResponse>>.Failure(userIdResult.Errors.ToArray());

            var bookingsQuery =
                from b in _context.Query<Booking>()
                where b.UserId == userIdResult.Value
                join e in _context.Query<Event>() on b.EventId equals e.Id
                join tt in _context.Query<TicketType>() on b.TicketTypeId equals tt.Id
                select new BookingByUserResponse(
                    b.Id.Value,
                    b.EventId.Value,
                    e.EventName.Value,
                    e.Date,
                    e.Location.City,
                    tt.TicketTypeName,
                    b.Quantity,
                    b.TotalAmount,
                    tt.Price.Currency,
                    b.Status.ToString(),
                    b.BookingDate);

            var bookings = await _context.ToListAsync(bookingsQuery, ct);

            return Result<List<BookingByUserResponse>>.Success(bookings);
        }
    }
}
