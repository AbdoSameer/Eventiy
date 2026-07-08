using Application.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Events;
using Domain.Common;
using Microsoft.Extensions.Logging;

namespace Application.EventHandlers
{
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
                    "Event {EventId} with key {IdempotencyKey} already processed - skipping",
                    notification.Id, notification.IdempotencyKey);
                return Result.Success();
            }

            _logger.LogInformation(
                "Booking {BookingId} cancelled — seat release handled by command handler.",
                notification.BookingId);

            await _idempotencyStore.MarkAsProcessedAsync(
                notification.Id,
                notification.IdempotencyKey,
                notification.OccurredOnUtc,
                cancellationToken);

            return Result.Success();
        }
    }
}
