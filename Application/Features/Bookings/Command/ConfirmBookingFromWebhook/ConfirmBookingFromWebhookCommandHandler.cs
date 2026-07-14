using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;

namespace Application.Features.Bookings.Command.ConfirmBookingFromWebhook;

public class ConfirmBookingFromWebhookCommandHandler
    : ICommandHandler<ConfirmBookingFromWebhookCommand, bool>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly TimeProvider _dateTimeProvider;
    private readonly ICacheService _cache;

    public ConfirmBookingFromWebhookCommandHandler(
        IBookingRepository bookingRepository,
        IEventRepository eventRepository,
        IUnitOfWork unitOfWork,
        IIdempotencyStore idempotencyStore,
        TimeProvider dateTimeProvider,
        ICacheService cache)
    {
        _bookingRepository = bookingRepository;
        _eventRepository = eventRepository;
        _unitOfWork = unitOfWork;
        _idempotencyStore = idempotencyStore;
        _dateTimeProvider = dateTimeProvider;
        _cache = cache;
    }

    public async Task<Result<bool>> Handle(
        ConfirmBookingFromWebhookCommand request,
        CancellationToken cancellationToken)
    {
        var stripeEventKey = $"stripe-webhook:{request.StripeEventId}";

        if (await _idempotencyStore.IsProcessedAsync(
            DeterministicGuid(request.StripeEventId), cancellationToken))
        {
            return Result<bool>.Success(true);
        }

        var bookingIdResult = BookingId.Create(request.BookingId);
        if (bookingIdResult.IsFailure)
            return Result<bool>.Failure(bookingIdResult.Errors.ToArray());

        var booking = await _bookingRepository.GetByIdAsync(bookingIdResult.Value, cancellationToken);
        if (booking is null)
            return Result<bool>.Failure(BookingErrors.BookingNotFound(bookingIdResult.Value));

        if (booking.Status == Domain.Aggregates.BookingAggregate.Enums.BookingStatusEnum.Confirmed)
            return Result<bool>.Success(true);

        var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;

        var confirmResult = booking.Confirm(utcNow);
        if (confirmResult.IsFailure)
            return Result<bool>.Failure(confirmResult.Errors.ToArray());

        var eventResult = await _eventRepository.GetByIdAsync(booking.EventId, cancellationToken);
        if (eventResult is null)
            return Result<bool>.Failure(EventErrors.EventNotFound(booking.EventId));

        var confirmSeatsResult = eventResult.ConfirmReservation(
            booking.TicketTypeId,
            booking.Quantity,
            utcNow);
        if (confirmSeatsResult.IsFailure)
            return Result<bool>.Failure(confirmSeatsResult.Errors.ToArray());

        _idempotencyStore.MarkAsProcessed(
            DeterministicGuid(request.StripeEventId),
            stripeEventKey,
            utcNow);

        var rowsAffected = await _unitOfWork.CommitAsync(cancellationToken);
        if (rowsAffected <= 0)
            return Result<bool>.Failure(BookingErrors.BookingConfirmationFailed());

        await _cache.RemoveAsync($"event:details:{booking.EventId.Value}", cancellationToken);

        return Result<bool>.Success(true);
    }

    private static Guid DeterministicGuid(string stripeEventId)
    {
        var hash = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(stripeEventId));
        return new Guid(hash);
    }
}
