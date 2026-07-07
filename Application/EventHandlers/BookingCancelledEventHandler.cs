using Application.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Events;
using Domain.Common;
using Domain.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.EventHandlers
{
    public class BookingCancelledEventHandler : IDomainEventHandler<BookingCancelledEvent>
    {
        private readonly IEventRepository _eventRepository;
        private readonly IIdempotencyStore _idempotencyStore;
        private readonly ILogger<BookingCancelledEventHandler> _logger;

        public BookingCancelledEventHandler(
            IEventRepository eventRepository,
            IIdempotencyStore idempotencyStore,
            ILogger<BookingCancelledEventHandler> logger)
        {
            _eventRepository = eventRepository;
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

            var @event = await _eventRepository.GetByIdAsync(notification.EventId, cancellationToken);

            if (@event is null)
            {
                _logger.LogWarning(
                    "Event {EventId} not found while releasing capacity for cancelled booking {BookingId}.",
                    notification.EventId, notification.BookingId);
                return Result.Success();
            }

            var result = @event.ReserveSeats(notification.TicketTypeId,
                                             notification.Quantity,
                                             notification.OccurredOnUtc,
                                             notification.Metadata);

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to release ticket capacity for booking {BookingId}: {Error}",
                    notification.BookingId, result);
                return result;
            }

            await _idempotencyStore.MarkAsProcessedAsync(
                notification.Id,
                notification.IdempotencyKey,
                notification.OccurredOnUtc,
                cancellationToken);

            return Result.Success();
        }
    }
}
