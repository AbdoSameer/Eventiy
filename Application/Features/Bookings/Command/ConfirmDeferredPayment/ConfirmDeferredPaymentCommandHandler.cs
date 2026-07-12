using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Common;
using Domain.Errors;

namespace Application.Features.Bookings.Command.ConfirmDeferredPayment;

public sealed class ConfirmDeferredPaymentCommandHandler(
    IBookingRepository bookingRepo,
    IEventRepository eventRepo,
    IUnitOfWork uow,
    TimeProvider timeProvider,
    ICacheService cache)
    : ICommandHandler<ConfirmDeferredPaymentCommand>
{
    public async Task<Result> Handle(ConfirmDeferredPaymentCommand request, CancellationToken cancellationToken)
    {
        var booking = await bookingRepo.GetByReferenceCodeAsync(request.ReferenceCode, cancellationToken);
        if (booking is null)
            return Result.Failure(BookingErrors.ReferenceCodeNotFound(request.ReferenceCode));

        if (booking.PaymentMethod != PaymentMethod.Deferred)
            return Result.Failure(BookingErrors.NotADeferredBooking(booking.Id.Value));

        if (booking.Status != BookingStatusEnum.Pending)
            return Result.Failure(BookingErrors.BookingNotPending(booking.Id.Value, booking.Status));

        var evt = await eventRepo.GetByIdAsync(booking.EventId, cancellationToken);
        if (evt is null)
            return Result.Failure(EventErrors.EventNotFound(booking.EventId));

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var confirmResult = booking.Confirm(utcNow);
        if (confirmResult.IsFailure)
            return confirmResult;

        var seatResult = evt.ConfirmReservation(booking.TicketTypeId, booking.Quantity, utcNow);
        if (seatResult.IsFailure)
            return seatResult;

        var rows = await uow.CommitAsync(cancellationToken);
        if (rows <= 0)
            return Result.Failure(BookingErrors.BookingConfirmationFailed());

        await cache.RemoveAsync($"event:details:{booking.EventId.Value}", cancellationToken);

        return Result.Success();
    }
}
