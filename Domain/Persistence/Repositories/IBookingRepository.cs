using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;

namespace Domain.Persistence.Repositories;

public interface IBookingRepository
{
    Task<Booking> AddBookingAsync(
        Booking booking,
        CancellationToken cancellationToken);

    Task<Booking?> GetByIdAsync(BookingId id, CancellationToken ct = default);

    Task<int> GetTotalReservedAsync(EventId eventId, CancellationToken ct = default);
}