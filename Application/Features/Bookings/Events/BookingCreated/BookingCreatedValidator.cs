using Application.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.BookingAggregate.Events;
using Domain.Common;
using Domain.Errors;

namespace Application.Features.Bookings.Events.BookingCreated;

internal sealed class BookingCreatedValidator : IEventValidator<BookingCreatedEvent>
{
    private readonly IApplicationReadDbContext _dbContext;

    public BookingCreatedValidator(IApplicationReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> ValidateAsync(
        BookingCreatedEvent @event,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Query<Booking>().Where(b => b.Id == @event.BookingId);
        var booking = await _dbContext.FirstOrDefaultAsync(query, cancellationToken);

        if (booking is null || booking.Status != BookingStatusEnum.Pending)
            return Result.Failure(BookingErrors.BookingNotFound(@event.BookingId.Value));

        return Result.Success();
    }
}
