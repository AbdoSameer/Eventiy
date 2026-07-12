using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.Entities;
using Domain.Aggregates.UserAggregate;
using Domain.Common;

namespace Application.Features.Bookings.Query.GetAllBookings;

internal sealed class GetAllBookingsQueryHandler(
    IApplicationReadDbContext _context)
    : IQueryHandler<GetAllBookingsQuery, List<GetAllBookingsResponse>>
{
    public async Task<Result<List<GetAllBookingsResponse>>> Handle(
        GetAllBookingsQuery query, CancellationToken ct)
    {
        var bookings = _context.Query<Booking>();

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<BookingStatusEnum>(query.Status, ignoreCase: true, out var statusFilter))
        {
            bookings = bookings.Where(b => b.Status == statusFilter);
        }

        var resultQuery =
            from b in bookings
            join e in _context.Query<Event>() on b.EventId equals e.Id
            join tt in _context.Query<TicketType>() on b.TicketTypeId equals tt.Id
            join u in _context.Query<User>() on b.UserId equals u.Id
            select new GetAllBookingsResponse(
                b.Id.Value,
                b.EventId.Value,
                b.UserId.Value,
                e.EventName.Value,
                u.FirstName + " " + u.LastName,
                tt.TicketTypeName,
                b.Quantity,
                b.TotalAmount,
                tt.Price.Currency,
                b.Status.ToString(),
                b.PaymentMethod.ToString(),
                b.ReferenceCode,
                b.BookingDate,
                b.HoldExpiresAt
            );

        var result = await _context.ToListAsync(resultQuery, ct);
        return Result<List<GetAllBookingsResponse>>.Success(result);
    }
}
