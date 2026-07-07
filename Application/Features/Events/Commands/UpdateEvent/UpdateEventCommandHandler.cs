using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Persistence.Repositories;
using Domain.Primitives;

namespace Application.Features.Events.Commands.UpdateEvent;

public sealed class UpdateEventCommandHandler(
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    TimeProvider dateTimeProvider,
    IEventMetadataFactory metadataFactory,
    ICacheService cache) : ICommandHandler<UpdateEventCommand>
{
    public async Task<Result> Handle(UpdateEventCommand request, CancellationToken cancellationToken)
    {
        var eventIdResult = EventId.Create(request.EventId);
        if (eventIdResult.IsFailure)
            return Result.Failure(eventIdResult.Errors.ToArray());

        var @event = await eventRepository.GetByIdAsync(eventIdResult.Value, cancellationToken);
        if (@event is null)
            return Result.Failure(EventErrors.EventNotFound(eventIdResult.Value));

        var locationResult = Address.Create(
            request.Location.Country,
            request.Location.City,
            request.Location.Street);
        if (locationResult.IsFailure)
            return Result.Failure(locationResult.Errors.ToArray());

        var metadata = metadataFactory.Create();
        var utcNow = dateTimeProvider.GetUtcNow().UtcDateTime;

        var updateNameResult = @event.UpdateName(request.Name, utcNow);
        if (updateNameResult.IsFailure)
            return Result.Failure(updateNameResult.Errors.ToArray());

        var updateCapacityResult = @event.UpdateCapacity(request.Capacity, utcNow, metadata);
        if (updateCapacityResult.IsFailure)
            return Result.Failure(updateCapacityResult.Errors.ToArray());

        var updateDateResult = @event.UpdateDate(request.Date, utcNow);
        if (updateDateResult.IsFailure)
            return Result.Failure(updateDateResult.Errors.ToArray());

        var updateLocationResult = @event.UpdateLocation(locationResult.Value, utcNow);
        if (updateLocationResult.IsFailure)
            return Result.Failure(updateLocationResult.Errors.ToArray());

        var updateDescriptionResult = @event.UpdateDescription(request.Description, utcNow);
        if (updateDescriptionResult.IsFailure)
            return Result.Failure(updateDescriptionResult.Errors.ToArray());

        eventRepository.Update(@event);

        var rows = await unitOfWork.CommitAsync(cancellationToken);
        if (rows <= 0)
            return Result.Failure(Error.Failure("Event.UpdateFailed", "Failed to update the event."));

        await cache.RemoveAsync($"event:details:{request.EventId}", cancellationToken);
        await cache.RemoveByPatternAsync("events:list:*", cancellationToken);

        return Result.Success();
    }
}
