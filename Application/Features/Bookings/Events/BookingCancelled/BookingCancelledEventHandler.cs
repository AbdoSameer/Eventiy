using Application.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Events;
using Domain.Common;
using Microsoft.Extensions.Logging;

namespace Application.Features.Bookings.Events.BookingCancelled;

public class BookingCancelledEventHandler : IDomainEventHandler<BookingCancelledEvent>
{
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BookingCancelledEventHandler> _logger;

    public BookingCancelledEventHandler(
        IIdempotencyStore idempotencyStore,
        IUnitOfWork uow,
        TimeProvider timeProvider,
        ILogger<BookingCancelledEventHandler> logger)
    {
        _idempotencyStore = idempotencyStore;
        _uow = uow;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(
        BookingCancelledEvent notification,
        CancellationToken cancellationToken = default)
    {
        if (notification.BookingId is null)
        {
            _logger.LogError("BookingId is null in BookingCancelledEvent — skipping");
            return Result.Failure(Error.Failure(
                "Event.NullBookingId",
                "BookingId Value Object was not deserialized correctly."));
        }

        var bookingIdValue = notification.BookingId.Value;
        var idempotencyKey = $"booking-cancelled:{bookingIdValue}";

        if (await _idempotencyStore.IsProcessedAsync(bookingIdValue, cancellationToken))
        {
            _logger.LogInformation(
                "BookingCancelledEvent for {BookingId} already processed — skipping (idempotent)",
                bookingIdValue);
            return Result.Success();
        }

        _logger.LogInformation(
            "Booking {BookingId} cancelled — seat release already handled by command handler",
            bookingIdValue);

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        _idempotencyStore.MarkAsProcessed(bookingIdValue, idempotencyKey, utcNow);

        await _uow.CommitWithoutEventsAsync(cancellationToken);

        return Result.Success();
    }
}
