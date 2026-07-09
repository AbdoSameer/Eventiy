using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class BookingRepository : IBookingRepository
    {
        private readonly ApplicationDbContext _applicationDbContext;

        public BookingRepository(ApplicationDbContext applicationDbContext)
        {
            _applicationDbContext = applicationDbContext;
        }

        public Task AddBookingAsync(Booking booking, CancellationToken cancellationToken)
        {
            _applicationDbContext.Bookings.Add(booking);
            return Task.CompletedTask;
        }

        public Task<Booking?> GetByIdAsync(BookingId id, CancellationToken ct = default)
        {
            return _applicationDbContext.Bookings
                     .FirstOrDefaultAsync(e => e.Id == id, ct);

        }

        public async Task<IReadOnlyList<Booking>> GetExpiredPendingBookingsAsync(
            DateTime utcNow,
            int batchSize,
            CancellationToken ct = default)
        {
            return await _applicationDbContext.Bookings
                .Where(b => b.Status == BookingStatusEnum.Pending
                         && b.HoldExpiresAt.HasValue
                         && b.HoldExpiresAt.Value <= utcNow)
                .OrderBy(b => b.HoldExpiresAt)
                .Take(batchSize)
                .ToListAsync(ct);
        }
    }
}
