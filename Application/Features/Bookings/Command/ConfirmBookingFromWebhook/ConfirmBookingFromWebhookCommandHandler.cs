using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Bookings.Command.ConfirmBookingFromWebhook;

public class ConfirmBookingFromWebhookCommandHandler
    : ICommandHandler<ConfirmBookingFromWebhookCommand, bool>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _dateTimeProvider;
    private readonly ICacheService _cache;

    public ConfirmBookingFromWebhookCommandHandler(
        IServiceScopeFactory scopeFactory,
        TimeProvider dateTimeProvider,
        ICacheService cache)
    {
        _scopeFactory = scopeFactory;
        _dateTimeProvider = dateTimeProvider;
        _cache = cache;
    }

    public async Task<Result<bool>> Handle(
        ConfirmBookingFromWebhookCommand request,
        CancellationToken cancellationToken)
    {
        var stripeEventKey = $"stripe-webhook:{request.StripeEventId}";

        using var preScope = _scopeFactory.CreateScope();
        var idempotencyStore = preScope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

        if (await idempotencyStore.IsProcessedAsync(
            DeterministicGuid(request.StripeEventId), cancellationToken))
        {
            return Result<bool>.Success(true);
        }

        var bookingIdResult = BookingId.Create(request.BookingId);
        if (bookingIdResult.IsFailure)
            return Result<bool>.Failure(bookingIdResult.Errors.ToArray());

        return await ConcurrencyRetryHelper.ExecuteWithConcurrencyRetryAsync(
            () => AttemptConfirmFromWebhook(bookingIdResult.Value, request.StripeEventId, stripeEventKey, cancellationToken),
            cancellationToken);
    }

    private async Task<Result<bool>> AttemptConfirmFromWebhook(
        BookingId bookingId,
        string stripeEventId,
        string stripeEventKey,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var eventRepo = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var idempotencyStore = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

        var booking = await bookingRepo.GetByIdAsync(bookingId, cancellationToken);
        if (booking is null)
            return Result<bool>.Failure(BookingErrors.BookingNotFound(bookingId.Value));

        if (booking.Status == Domain.Aggregates.BookingAggregate.Enums.BookingStatusEnum.Confirmed)
            return Result<bool>.Success(true);

        var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;

        var confirmResult = booking.Confirm(utcNow);
        if (confirmResult.IsFailure)
            return Result<bool>.Failure(confirmResult.Errors.ToArray());

        var eventResult = await eventRepo.GetByIdAsync(booking.EventId, cancellationToken);
        if (eventResult is null)
            return Result<bool>.Failure(EventErrors.EventNotFound(booking.EventId));

        var confirmSeatsResult = eventResult.ConfirmReservation(
            booking.TicketTypeId,
            booking.Quantity,
            utcNow);
        if (confirmSeatsResult.IsFailure)
            return Result<bool>.Failure(confirmSeatsResult.Errors.ToArray());

        uow.EnforceFencingToken(eventResult, eventResult.RowVersion);

        idempotencyStore.MarkAsProcessed(
            DeterministicGuid(stripeEventId),
            stripeEventKey,
            utcNow);

        var rowsAffected = await uow.CommitAsync(cancellationToken);
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
