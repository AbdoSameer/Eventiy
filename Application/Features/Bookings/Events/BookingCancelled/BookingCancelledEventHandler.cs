using Application.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Events;
using Domain.Common;
using Microsoft.Extensions.Logging;

namespace Application.Features.Bookings.Events.BookingCancelled;

public class BookingCancelledEventHandler : IDomainEventHandler<BookingCancelledEvent>
{
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<BookingCancelledEventHandler> _logger;

    public BookingCancelledEventHandler(
        IIdempotencyStore idempotencyStore,
        ILogger<BookingCancelledEventHandler> logger)
    {
        _idempotencyStore = idempotencyStore;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(BookingCancelledEvent notification, CancellationToken cancellationToken = default)
    {
        if (await _idempotencyStore.IsProcessedAsync(notification.Id, cancellationToken))
        {
            _logger.LogInformation(
                "Event {EventId} already processed - skipping",
                notification.Id);
            return Result.Success();
        }

        _logger.LogInformation(
            "Booking {BookingId} cancelled — seat release handled by command handler.",
            notification.BookingId);

        await _idempotencyStore.MarkAsProcessedAsync(
            notification.Id,
            notification.Id.ToString("N"),
            notification.OccurredOnUtc,
            cancellationToken);

        return Result.Success();
    }
}
