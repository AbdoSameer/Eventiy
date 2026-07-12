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
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BookingCreatedEventHandler> _logger;

    public BookingCreatedEventHandler(
        IBookingRepository bookingRepo,
        IEventRepository eventRepo,
        IUnitOfWork uow,
        TimeProvider timeProvider,
        ILogger<BookingCreatedEventHandler> logger)
    {
        _bookingRepo = bookingRepo;
        _eventRepo = eventRepo;
        _uow = uow;
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
        // Only Instant payments are confirmed automatically.
        if (booking.PaymentMethod == PaymentMethod.Deferred)
        {
            _logger.LogInformation(
                "Booking {BookingId} is Deferred ({ReferenceCode}) — awaiting external payment confirmation",
                bookingIdValue, booking.ReferenceCode);
            return Result.Success();
        }

        var evt = await _eventRepo.GetByIdAsync(@event.EventId, cancellationToken);
        if (evt is null)
        {
            _logger.LogError("Event {EventId} not found — cannot confirm booking {BookingId}",
                @event.EventId?.Value ?? Guid.Empty, bookingIdValue);
            return Result.Failure(EventErrors.EventNotFound(@event.EventId));
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var confirmResult = booking.Confirm(utcNow);
        if (confirmResult.IsFailure)
            return confirmResult;

        var seatResult = evt.ConfirmReservation(@event.TicketTypeId, @event.Quantity, utcNow);
        if (seatResult.IsFailure)
            return seatResult;

        var rows = await _uow.CommitAsync(cancellationToken);
        if (rows <= 0)
        {
            _logger.LogError("Commit returned 0 rows — booking {BookingId} not confirmed",
                bookingIdValue);
            return Result.Failure(BookingErrors.BookingConfirmationFailed());
        }

        _logger.LogInformation(
            "Booking {BookingId} confirmed (Instant) — {Quantity} seat(s) moved from Reserved to Sold",
            bookingIdValue, @event.Quantity);

        return Result.Success();
    }
}
