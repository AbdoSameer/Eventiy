using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Application.Abstractions.Inventory;

/// <summary>
/// Reservation context handed to an <see cref="IInventoryReservationStrategy"/>.
/// Carries everything a strategy needs to reserve seats without the handler
/// knowing whether the reservation is backed by SQL RowVersion or Redis DECR.
/// </summary>
public sealed record ReservationContext(
    Event Event,
    TicketTypeId TicketTypeId,
    int Quantity,
    DateTime UtcNow);

/// <summary>
/// Outcome of a reservation attempt. Strategies that succeed return
/// <see cref="ReservationResult.Success"/> and let the caller persist the
/// booking. Strategies that fail return the typed Result.
/// </summary>
public sealed record ReservationResult
{
    public bool Succeeded { get; init; }

    /// <summary>
    /// For the Atomic Redis strategy: the remaining count reported by Redis
    /// after the decrement. For the optimistic strategy this is null
    /// (the aggregate owns the count).
    /// </summary>
    public long? RedisRemainingCount { get; init; }

    public static ReservationResult Success(long? redisRemainingCount = null) =>
        new() { Succeeded = true, RedisRemainingCount = redisRemainingCount };
}

/// <summary>
/// Strategy abstraction for reserving seats on an Event.
/// Implementations decide the concurrency-control mechanism:
/// <list type="bullet">
/// <item><see cref="OptimisticReservationStrategy"/> — SQL RowVersion.</item>
/// <item><see cref="AtomicRedisReservationStrategy"/> — Redis DECR + outbox sync.</item>
/// </list>
/// The handler branches on <see cref="Event.IsHighDemand"/> to pick one.
/// </summary>
public interface IInventoryReservationStrategy
{
    /// <summary>
    /// Reserves <paramref name="quantity"/> seats on the given ticket type.
    /// </summary>
    /// <returns>
    /// Success with the reservation result, or Failure with typed errors.
    /// Implementations may throw <see cref="Domain.Common.ConcurrencyException"/>
    /// to signal a retryable concurrency conflict to the caller's
    /// <c>ConcurrencyRetryHelper</c>.
    /// </returns>
    Task<Result<ReservationResult>> ReserveAsync(
        ReservationContext context,
        CancellationToken cancellationToken = default);
}
