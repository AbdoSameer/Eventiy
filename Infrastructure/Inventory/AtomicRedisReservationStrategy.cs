using Application.Abstractions.Inventory;
using Domain.Common;
using Domain.Errors;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Infrastructure.Inventory;

/// <summary>
/// Atomic Redis-backed reservation for high-demand events.
///
/// Flow:
/// 1. Lazily seed the Redis counter from the aggregate's available count
///    using SETNX (idempotent — only the first writer wins).
/// 2. Atomically DECRBY the counter via <see cref="IDatabase.StringDecrementAsync"/>.
/// 3. If the result is &gt;= 0 the reservation succeeded — raise a
///    <see cref="Domain.Aggregates.EventAggregate.Events.TicketTypeEvents.TicketTypeRedisReservationSyncedEvent"/>
///    domain event on the aggregate (via <c>Event.RecordRedisReservation</c>)
///    so the UnitOfWork outbox queues a sync-back message to SQL.
/// 4. If the result is &lt; 0 the event is sold out — undo the decrement and
///    return a typed shortfall error (no state mutation on the aggregate).
/// 5. If Redis is unreachable, return <see cref="EventErrors.RedisInventoryUnavailable"/>
///    so the handler can surface a controlled failure instead of crashing.
/// </summary>
public sealed class AtomicRedisReservationStrategy : IInventoryReservationStrategy
{
    private const string InventoryKeyPrefix = "inv:ticket:";

    private readonly Func<IDatabase> _databaseFactory;
    private readonly ILogger<AtomicRedisReservationStrategy> _logger;

    /// <summary>
    /// Constructor used in production. Resolves the database from the
    /// registered <see cref="ConnectionMultiplexer"/> singleton.
    /// </summary>
    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public AtomicRedisReservationStrategy(
        ConnectionMultiplexer redis,
        ILogger<AtomicRedisReservationStrategy> logger)
        : this(() => redis.GetDatabase(), logger)
    {
    }

    /// <summary>
    /// Constructor used in tests to inject a fake <see cref="IDatabase"/>.
    /// </summary>
    public AtomicRedisReservationStrategy(
        Func<IDatabase> databaseFactory,
        ILogger<AtomicRedisReservationStrategy> logger)
    {
        _databaseFactory = databaseFactory;
        _logger = logger;
    }

    public async Task<Result<ReservationResult>> ReserveAsync(
        ReservationContext context,
        CancellationToken cancellationToken = default)
    {
        var ticketType = context.Event.TicketTypes
            .FirstOrDefault(t => t.Id == context.TicketTypeId);

        if (ticketType is null)
            return Result<ReservationResult>.Failure(
                EventErrors.TicketTypeNotFound(context.TicketTypeId.Value));

        if (context.Quantity <= 0)
            return Result<ReservationResult>.Failure(
                Error.Validation("Inventory.Quantity", "Quantity must be greater than zero."));

        var key = InventoryKeyPrefix + context.TicketTypeId.Value;

        try
        {
            var db = _databaseFactory();

            await EnsureCounterSeededAsync(db, key, ticketType.AvailableCount);

            long remaining = await db.StringDecrementAsync(key, context.Quantity);

            if (remaining >= 0)
            {
                var recordResult = context.Event.RecordRedisReservation(
                    context.TicketTypeId,
                    context.Quantity,
                    remaining,
                    context.UtcNow);

                if (recordResult.IsFailure)
                {
                    await db.StringIncrementAsync(key, context.Quantity);
                    return Result<ReservationResult>.Failure(recordResult.Errors.ToArray());
                }

                _logger.LogInformation(
                    "Redis atomic reservation succeeded: ticket {TicketTypeId}, qty {Quantity}, remaining {Remaining}",
                    context.TicketTypeId.Value, context.Quantity, remaining);

                return Result<ReservationResult>.Success(
                    ReservationResult.Success(remaining));
            }

            await db.StringIncrementAsync(key, context.Quantity);

            _logger.LogInformation(
                "Redis atomic reservation rejected (sold out): ticket {TicketTypeId}, requested {Quantity}, remaining {Remaining}",
                context.TicketTypeId.Value, context.Quantity, remaining);

            return Result<ReservationResult>.Failure(
                EventErrors.RedisInventoryShortfall(context.Quantity, remaining + context.Quantity));
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex,
                "Redis unavailable for ticket {TicketTypeId}; high-demand reservation rejected",
                context.TicketTypeId.Value);

            return Result<ReservationResult>.Failure(
                EventErrors.RedisInventoryUnavailable());
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex,
                "Redis timeout for ticket {TicketTypeId}; high-demand reservation rejected",
                context.TicketTypeId.Value);

            return Result<ReservationResult>.Failure(
                EventErrors.RedisInventoryUnavailable());
        }
    }

    /// <summary>
    /// Seeds the Redis counter with the ticket's current available count
    /// only if the key does not already exist. SETNX makes this idempotent:
    /// the first caller wins, subsequent callers just skip. This means the
    /// very first reservation pays one extra round-trip, after which the
    /// counter is the source of truth for availability.
    /// </summary>
    private static Task EnsureCounterSeededAsync(IDatabase db, string key, int availableCount)
    {
        // StringSetAsync with when = When.NotExists is the SETNX primitive.
        // We seed with the current available count from SQL; if another
        // instance already seeded, this is a no-op.
        return db.StringSetAsync(
            key,
            availableCount,
            when: When.NotExists);
    }
}

