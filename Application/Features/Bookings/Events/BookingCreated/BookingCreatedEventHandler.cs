using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.BookingAggregate.Events;
using Domain.Common;
using Domain.Errors;
using Microsoft.Extensions.Logging;

namespace Application.Features.Bookings.Events.BookingCreated;

public class BookingCreatedEventHandler : IDomainEventHandler<BookingCreatedEvent>
{
    private readonly IBookingRepository _bookingRepo;
    private readonly IEventRepository _eventRepo;
    private readonly IUnitOfWork _uow;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BookingCreatedEventHandler> _logger;

    public BookingCreatedEventHandler(
        IBookingRepository bookingRepo,
        IEventRepository eventRepo,
        IUnitOfWork uow,
        IIdempotencyStore idempotencyStore,
        TimeProvider timeProvider,
        ILogger<BookingCreatedEventHandler> logger)
    {
        _bookingRepo = bookingRepo;
        _eventRepo = eventRepo;
        _uow = uow;
        _idempotencyStore = idempotencyStore;
        _timeProvider = timeProvider;
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
            _logger.LogWarning("Booking {BookingId} not found — skipping", bookingIdValue);
            return Result.Success();
        }

        if (booking.Status != BookingStatusEnum.Pending)
        {
            _logger.LogInformation("Booking {BookingId} already {Status} — skipping",
                bookingIdValue, booking.Status);
            return Result.Success();
        }

        // Deferred payments await manual confirmation (Fawry/Cash callback).
        // Instant payments await Stripe webhook confirmation (checkout.session.completed).
        // Neither payment method is auto-confirmed here — the webhook is the source of truth.
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        _logger.LogInformation(
            "Booking {BookingId} created with {PaymentMethod} — awaiting external confirmation (webhook/deferred)",
            bookingIdValue, booking.PaymentMethod);

        _idempotencyStore.MarkAsProcessed(bookingIdValue, idempotencyKey, utcNow);
        await _uow.CommitWithoutEventsAsync(cancellationToken);

        return Result.Success();
    }
}
