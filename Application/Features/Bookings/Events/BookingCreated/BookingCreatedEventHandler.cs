using Application.Abstractions.Caching;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.BookingAggregate.Events;
using Domain.Common;
using Domain.Errors;
using Microsoft.Extensions.Logging;
using static Application.Abstractions.Caching.CacheKeys;

namespace Application.Features.Bookings.Events.BookingCreated;

public class BookingCreatedEventHandler : IDomainEventHandler<BookingCreatedEvent>
{
    private readonly IBookingRepository _bookingRepo;
    private readonly IUnitOfWork _uow;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly TimeProvider _timeProvider;
    private readonly ICacheService _cache;
    private readonly ILogger<BookingCreatedEventHandler> _logger;

    public BookingCreatedEventHandler(
        IBookingRepository bookingRepo,
        IUnitOfWork uow,
        IIdempotencyStore idempotencyStore,
        TimeProvider timeProvider,
        ICacheService cache,
        ILogger<BookingCreatedEventHandler> logger)
    {
        _bookingRepo = bookingRepo;
        _uow = uow;
        _idempotencyStore = idempotencyStore;
        _timeProvider = timeProvider;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(
        BookingCreatedEvent @event,
        CancellationToken cancellationToken = default)
    {
        if (@event is null)
        {
            _logger.LogError("BookingCreatedEvent is null — skipping");
            return Result.Failure(Error.Failure("Event.Null", "Received null event"));
        }

        if (@event.BookingId is null)
        {
            _logger.LogError("BookingId is null in BookingCreatedEvent — skipping. " +
                             "This indicates a JSON deserialization failure of the BookingId Value Object.");
            return Result.Failure(Error.Failure(
                "Event.NullBookingId",
                "BookingId Value Object was not deserialized correctly. " +
                "Ensure EventSerializer has BookingIdJsonConverter registered."));
        }

        var bookingIdValue = @event.BookingId.Value;
        if (bookingIdValue == Guid.Empty)
        {
            _logger.LogError("BookingId.Value is Guid.Empty — deserialized BookingId was not populated. " +
                             "Event payload may be corrupted or missing bookingId property.");
            return Result.Failure(Error.Failure(
                "Event.EmptyBookingId",
                "BookingId Value Object has an empty GUID. Check outbox payload for bookingId."));
        }

        var idempotencyKey = $"booking-created:{bookingIdValue}";

        if (await _idempotencyStore.IsProcessedAsync(bookingIdValue, cancellationToken))
        {
            _logger.LogInformation(
                "BookingCreatedEvent for {BookingId} already processed — skipping (idempotent)",
                bookingIdValue);
            return Result.Success();
        }

        var booking = await _bookingRepo.GetByIdAsync(@event.BookingId, cancellationToken);
        if (booking is null)
        {
            _logger.LogWarning("Booking {BookingId} not found — will retry", bookingIdValue);
            return Result.Failure(BookingErrors.BookingNotFound(bookingIdValue));
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        if (booking.Status != BookingStatusEnum.Pending)
        {
            _logger.LogInformation("Booking {BookingId} already {Status} — marking processed and skipping",
                bookingIdValue, booking.Status);
            _idempotencyStore.MarkAsProcessed(bookingIdValue, idempotencyKey, utcNow);
            await _uow.CommitWithoutEventsAsync(cancellationToken);
            return Result.Success();
        }

        _logger.LogInformation(
            "Booking {BookingId} created — Event {EventId}, TicketType {TicketTypeId}, Qty {Quantity}, {Amount} {Currency}",
            bookingIdValue, @event.EventId, @event.TicketTypeId, @event.Quantity,
            @event.Money.Amount, @event.Money.Currency);

        _idempotencyStore.MarkAsProcessed(bookingIdValue, idempotencyKey, utcNow);
        await _uow.CommitWithoutEventsAsync(cancellationToken);

        // Invalidate cached event details so seat availability reflects new booking
        await _cache.RemoveAsync(EventDetails(@event.EventId.Value), cancellationToken);

        return Result.Success();
    }
}
