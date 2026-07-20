using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Microsoft.Data.SqlClient;
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
            var sql = @"
                SELECT TOP (@BatchSize) b.*
                FROM Bookings b WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE b.Status IN ('Pending', 'PendingPayment')
                  AND b.HoldExpiresAt IS NOT NULL
                  AND b.HoldExpiresAt <= @Now
                ORDER BY b.HoldExpiresAt";

            var parameters = new[]
            {
                new SqlParameter("@BatchSize", batchSize),
                new SqlParameter("@Now", utcNow)
            };

            return await _applicationDbContext.Bookings
                .FromSqlRaw(sql, parameters)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Booking>> GetPendingInstantBookingsPastHoldAsync(
            DateTime utcNow,
            int batchSize,
            CancellationToken ct = default)
        {
            var sql = @"
                SELECT TOP (@BatchSize) b.*
                FROM Bookings b WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE b.Status IN ('Pending', 'PendingPayment')
                  AND b.PaymentMethod = 'Instant'
                  AND b.HoldExpiresAt IS NOT NULL
                  AND b.HoldExpiresAt <= @Now
                ORDER BY b.HoldExpiresAt";

            var parameters = new[]
            {
                new SqlParameter("@BatchSize", batchSize),
                new SqlParameter("@Now", utcNow)
            };

            return await _applicationDbContext.Bookings
                .FromSqlRaw(sql, parameters)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        public Task<Booking?> GetByReferenceCodeAsync(string referenceCode, CancellationToken ct = default)
        {
            return _applicationDbContext.Bookings
                .FirstOrDefaultAsync(b => b.ReferenceCode == referenceCode, ct);
        }

        public async Task<IReadOnlyList<Booking>> GetLatestPendingByTicketTypeAsync(
            TicketTypeId ticketTypeId,
            int batchSize,
            CancellationToken ct = default)
        {
            return await _applicationDbContext.Bookings
                .Where(b => b.TicketTypeId == ticketTypeId
                    && (b.Status == BookingStatusEnum.Pending
                        || b.Status == BookingStatusEnum.PendingPayment))
                .OrderByDescending(b => b.BookingDate)
                .Take(batchSize)
                .ToListAsync(ct);
        }
    }
}
