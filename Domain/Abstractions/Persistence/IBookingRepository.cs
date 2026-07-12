using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;

namespace Domain.Abstractions.Persistence;

public interface IBookingRepository
{
    Task AddBookingAsync(
        Booking booking,
        CancellationToken cancellationToken);

    Task<Booking?> GetByIdAsync(BookingId id, CancellationToken ct = default);

    Task<IReadOnlyList<Booking>> GetExpiredPendingBookingsAsync(
        DateTime utcNow,
        int batchSize,
        CancellationToken ct = default);

    Task<Booking?> GetByReferenceCodeAsync(
        string referenceCode,
        CancellationToken ct = default);
}