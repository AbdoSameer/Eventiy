using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;

namespace Domain.Abstractions.Persistence;

public interface IEventRepository
{
    Task AddEventAsync(
        Event @event,
        CancellationToken cancellationToken);

    Task<Event?> GetByIdAsync(
        EventId id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads the Event aggregate with its TicketTypes using a pessimistic
    /// lock (UPDLOCK, HOLDLOCK) so no concurrent reservation can modify the
    /// ticket-type rows until the current transaction commits. Used by the
    /// ToggleHighDemandCommandHandler to atomically read the inventory
    /// counts, force-seed Redis, and flip the IsHighDemand flag.
    /// </summary>
    Task<Event?> GetByIdWithLockAsync(
        EventId id,
        CancellationToken cancellationToken);

    Task<List<Event>> GetHighDemandEventsAsync(CancellationToken cancellationToken);
}