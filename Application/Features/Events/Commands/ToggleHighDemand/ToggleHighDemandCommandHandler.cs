using Application.Abstractions.Inventory;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Events.Commands.ToggleHighDemand;

/// <summary>
/// Atomically toggles the Event.IsHighDemand flag while holding a
/// pessimistic lock on the Event row (UPDLOCK, HOLDLOCK) and force-seeding
/// the Redis inventory counters. This is the "Atomic Handover" — Layer 1
/// of the three-layer defense against the Strategy Handover Race.
///
/// Flow:
/// 1. Begin an explicit DB transaction.
/// 2. Load the Event + TicketTypes with UPDLOCK, HOLDLOCK — blocks all
///    concurrent reservations from reading/modifying the rows.
/// 3. Call SetHighDemandMode on the aggregate (domain invariants).
/// 4. If enabling: force-seed Redis counters with SET (overwrites any
///    stale value from a previous session). If disabling: delete the
///    Redis keys so the next booking falls back to SQL.
/// 5. Commit the transaction — releases the pessimistic lock. The
///    RowVersion bump forces any in-flight booking that read the old
///    IsHighDemand=false to retry via fencing (Layer 2).
/// </summary>
public sealed class ToggleHighDemandCommandHandler
    : ICommandHandler<ToggleHighDemandCommand, ToggleHighDemandResponse>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IInventorySeeder _inventorySeeder;

    public ToggleHighDemandCommandHandler(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IInventorySeeder inventorySeeder)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _inventorySeeder = inventorySeeder;
    }

    public async Task<Result<ToggleHighDemandResponse>> Handle(
        ToggleHighDemandCommand request,
        CancellationToken cancellationToken)
    {
        var eventIdResult = EventId.Create(request.EventId);
        if (eventIdResult.IsFailure)
            return Result<ToggleHighDemandResponse>.Failure(eventIdResult.Errors.ToArray());

        using var scope = _scopeFactory.CreateScope();
        var eventRepo = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        Result<ToggleHighDemandResponse>? result = null;

        try
        {
            await uow.ExecuteInTransactionAsync(async (transaction, ct) =>
            {
                var @event = await eventRepo.GetByIdWithLockAsync(
                    eventIdResult.Value, ct);

                if (@event is null)
                {
                    result = Result<ToggleHighDemandResponse>.Failure(
                        EventErrors.EventNotFound(eventIdResult.Value));
                    return;
                }

                var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

                var toggleResult = @event.SetHighDemandMode(request.Enabled, utcNow);
                if (toggleResult.IsFailure)
                {
                    result = Result<ToggleHighDemandResponse>.Failure(toggleResult.Errors.ToArray());
                    return;
                }

                if (request.Enabled)
                    await _inventorySeeder.SeedAsync(@event, ct);
                else
                    await _inventorySeeder.ClearAsync(@event, ct);

                await uow.CommitAsync(ct);

                result = Result<ToggleHighDemandResponse>.Success(
                    new ToggleHighDemandResponse(@event.IsHighDemand));
            }, cancellationToken);

            return result!;
        }
        catch (ConcurrencyException)
        {
            return Result<ToggleHighDemandResponse>.Failure(
                EventErrors.EventModifiedConcurrently());
        }
    }
}
