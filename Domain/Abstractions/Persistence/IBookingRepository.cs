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

    Task<IReadOnlyList<Booking>> GetPendingInstantBookingsPastHoldAsync(
        DateTime utcNow,
        int batchSize,
        CancellationToken ct = default);

    Task<Booking?> GetByReferenceCodeAsync(
        string referenceCode,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent pending bookings for a given ticket type,
    /// ordered by creation date descending. Used by the
    /// InventoryReconciliationJob to cancel the latest oversold bookings
    /// when a split-brain inventory desync is detected.
    /// </summary>
    Task<IReadOnlyList<Booking>> GetLatestPendingByTicketTypeAsync(
        TicketTypeId ticketTypeId,
        int batchSize,
        CancellationToken ct = default);
}