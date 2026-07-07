using Application.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Events;
using Domain.Common;
using Microsoft.Extensions.Logging;

namespace Application.EventHandlers;

public class BookingCreatedEventHandler : IDomainEventHandler<BookingCreatedEvent>
{
    private readonly IEventValidator<BookingCreatedEvent> _validator;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<BookingCreatedEventHandler> _logger;

    public BookingCreatedEventHandler(
        IEventValidator<BookingCreatedEvent> validator,
        IIdempotencyStore idempotencyStore,
        ILogger<BookingCreatedEventHandler> logger)
    {
        _validator = validator;
        _idempotencyStore = idempotencyStore;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(
        BookingCreatedEvent @event,
        CancellationToken cancellationToken = default)
    {
        if (await _idempotencyStore.IsProcessedAsync(@event.Id, cancellationToken))
        {
            _logger.LogInformation(
                "Event {EventId} with key {IdempotencyKey} already processed - skipping",
                @event.Id,
                @event.IdempotencyKey);

            return Result.Success();
        }

        var validation = await _validator.ValidateAsync(@event, cancellationToken);
        if (validation.IsFailure)
            return validation;

        await _idempotencyStore.MarkAsProcessedAsync(
            @event.Id,
            @event.IdempotencyKey,
            @event.OccurredOnUtc,
            cancellationToken);

        return Result.Success();
    }
}
