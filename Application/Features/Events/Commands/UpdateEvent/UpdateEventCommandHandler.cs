using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Primitives;
using static Application.Abstractions.Caching.CacheKeys;

namespace Application.Features.Events.Commands.UpdateEvent;

public sealed class UpdateEventCommandHandler(
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    TimeProvider dateTimeProvider,
    ICacheService cache,
    ICurrentUserService currentUser) : ICommandHandler<UpdateEventCommand>
{
    public async Task<Result> Handle(UpdateEventCommand request, CancellationToken cancellationToken)
    {
        var isAdmin = currentUser.GetCurrentUserRole() == "Admin";

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

        var utcNow = dateTimeProvider.GetUtcNow().UtcDateTime;

        var updateNameResult = @event.UpdateName(request.Name, utcNow, isAdmin);
        if (updateNameResult.IsFailure)
            return Result.Failure(updateNameResult.Errors.ToArray());

        var updateCapacityResult = @event.UpdateCapacity(request.Capacity, utcNow, isAdmin);
        if (updateCapacityResult.IsFailure)
            return Result.Failure(updateCapacityResult.Errors.ToArray());

        var updateDateResult = @event.UpdateDate(request.Date, utcNow, isAdmin);
        if (updateDateResult.IsFailure)
            return Result.Failure(updateDateResult.Errors.ToArray());

        var updateLocationResult = @event.UpdateLocation(locationResult.Value, utcNow, isAdmin);
        if (updateLocationResult.IsFailure)
            return Result.Failure(updateLocationResult.Errors.ToArray());

        var updateDescriptionResult = @event.UpdateDescription(request.Description, utcNow, isAdmin);
        if (updateDescriptionResult.IsFailure)
            return Result.Failure(updateDescriptionResult.Errors.ToArray());

        var rows = await unitOfWork.CommitAsync(cancellationToken);
        if (rows <= 0)
            return Result.Failure(Error.Failure("Event.UpdateFailed", "Failed to update the event."));

        await cache.RemoveAsync(EventDetails(request.EventId), cancellationToken);
        await cache.RemoveByPatternAsync(EventsListPattern, cancellationToken);

        return Result.Success();
    }
}
