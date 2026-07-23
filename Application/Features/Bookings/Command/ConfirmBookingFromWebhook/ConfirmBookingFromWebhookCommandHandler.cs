using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using static Application.Abstractions.Caching.CacheKeys;

namespace Application.Features.Bookings.Command.ConfirmBookingFromWebhook;

public class ConfirmBookingFromWebhookCommandHandler
    : ICommandHandler<ConfirmBookingFromWebhookCommand, bool>
{
    private readonly TimeProvider _dateTimeProvider;
    private readonly ICacheService _cache;
    private readonly IBookingRepository _bookingRepo;
    private readonly IEventRepository _eventRepo;
    private readonly IUnitOfWork _uow;
    private readonly IIdempotencyStore _idempotencyStore;

    public ConfirmBookingFromWebhookCommandHandler(
        TimeProvider dateTimeProvider,
        ICacheService cache,
        IBookingRepository bookingRepo,
        IEventRepository eventRepo,
        IUnitOfWork uow,
        IIdempotencyStore idempotencyStore)
    {
        _dateTimeProvider = dateTimeProvider;
        _cache = cache;
        _bookingRepo = bookingRepo;
        _eventRepo = eventRepo;
        _uow = uow;
        _idempotencyStore = idempotencyStore;
    }

    public async Task<Result<bool>> Handle(
        ConfirmBookingFromWebhookCommand request,
        CancellationToken cancellationToken)
    {
        if (await _idempotencyStore.IsProcessedAsync(
            DeterministicGuid(request.StripeEventId), cancellationToken))
        {
            return Result<bool>.Success(true);
        }

        var bookingIdResult = BookingId.Create(request.BookingId);
        if (bookingIdResult.IsFailure)
            return Result<bool>.Failure(bookingIdResult.Errors.ToArray());

        var bookingId = bookingIdResult.Value;
        var stripeEventId = request.StripeEventId;

        var booking = await _bookingRepo.GetByIdAsync(bookingId, cancellationToken);
        if (booking is null)
            return Result<bool>.Failure(BookingErrors.BookingNotFound(bookingId.Value));

        if (booking.Status == Domain.Aggregates.BookingAggregate.Enums.BookingStatusEnum.Confirmed)
            return Result<bool>.Success(true);

        var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;

        var confirmResult = booking.Confirm(utcNow);
        if (confirmResult.IsFailure)
            return Result<bool>.Failure(confirmResult.Errors.ToArray());

        var eventResult = await _eventRepo.GetByIdAsync(booking.EventId, cancellationToken);
        if (eventResult is null)
            return Result<bool>.Failure(EventErrors.EventNotFound(booking.EventId));

        var confirmSeatsResult = eventResult.ConfirmReservation(
            booking.TicketTypeId,
            booking.Quantity,
            utcNow);
        if (confirmSeatsResult.IsFailure)
            return Result<bool>.Failure(confirmSeatsResult.Errors.ToArray());

        _uow.EnforceFencingToken(eventResult, eventResult.RowVersion);

        _idempotencyStore.MarkAsProcessed(
            DeterministicGuid(stripeEventId),
            $"stripe-webhook:{stripeEventId}",
            utcNow);

        var rowsAffected = await _uow.CommitAsync(cancellationToken);
        if (rowsAffected <= 0)
            return Result<bool>.Failure(BookingErrors.BookingConfirmationFailed());

        await _cache.RemoveAsync(EventDetails(booking.EventId.Value), cancellationToken);

        return Result<bool>.Success(true);
    }

    private static Guid DeterministicGuid(string stripeEventId)
        => Application.Abstractions.Caching.DeterministicGuid.FromStripeEventId(stripeEventId);
}
