using Application.Abstractions.Inventory;
using Domain.Common;
using Domain.Errors;

namespace Application.Features.Bookings.Inventory;

/// <summary>
/// Default strategy. Mutates the <see cref="Domain.Aggregates.EventAggregate.Event"/>
/// aggregate in memory (which bumps <c>ReservedCount</c> on the TicketType and
/// raises the <c>TicketTypeSeatsReservedEvent</c> domain event). The actual
/// concurrency check is deferred to <c>IUnitOfWork.CommitAsync</c>, which will
/// throw <see cref="ConcurrencyException"/> if the SQL <c>RowVersion</c> changed
/// under us. The handler's <c>ConcurrencyRetryHelper</c> turns that into a retry.
/// </summary>
public sealed class OptimisticReservationStrategy : IInventoryReservationStrategy
{
    public Task<Result<ReservationResult>> ReserveAsync(
        ReservationContext context,
        CancellationToken cancellationToken = default)
    {
        var reservationResult = context.Event.ReserveSeats(
            context.TicketTypeId,
            context.Quantity,
            context.UtcNow);

        if (reservationResult.IsFailure)
            return Task.FromResult(Result<ReservationResult>.Failure(reservationResult.Errors.ToArray()));

        return Task.FromResult(Result<ReservationResult>.Success(
            ReservationResult.Success()));
    }
}
