using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    public class BookingRepository :IBookingRepository
    {
        private readonly ApplicationDbContext _applicationDbContext;

        public BookingRepository(ApplicationDbContext applicationDbContext)
        {
            _applicationDbContext = applicationDbContext;
        }

        public Task<Booking> AddBookingAsync(Booking booking, CancellationToken cancellationToken)
        {
            var entityEntry = _applicationDbContext.Bookings.Add(booking);

            return Task.FromResult(entityEntry.Entity);
        }

        public Task<Booking?> GetByIdAsync(BookingId id, CancellationToken ct = default)
        {
            return _applicationDbContext.Bookings
                     .FirstOrDefaultAsync(e => e.Id == id, ct);
            
        }

        public async Task<int> GetTotalReservedAsync(EventId eventId, CancellationToken ct = default)
        {
            var result = await _applicationDbContext.Bookings
                                .Where(b => b.EventId == eventId
                                 && b.Status != BookingStatusEnum.Cancelled
                                 && b.Status != BookingStatusEnum.Expired)
                                .SumAsync(b => b.Quantity, ct);
            
            return (int)result;
        }
    }
}
