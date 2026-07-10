using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Primitives;

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
        var role = currentUser.GetCurrentUserRole();
        if (role != "Admin" && role != "Organizer")
            throw new UnauthorizedAccessException("Only administrators or organizers can update events.");

        var isAdmin = role == "Admin";

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

        if (isAdmin)
        {
            var updateNameResult = @event.AdminUpdateName(request.Name, utcNow);
            if (updateNameResult.IsFailure)
                return Result.Failure(updateNameResult.Errors.ToArray());

            var updateCapacityResult = @event.AdminUpdateCapacity(request.Capacity, utcNow);
            if (updateCapacityResult.IsFailure)
                return Result.Failure(updateCapacityResult.Errors.ToArray());

            var updateDateResult = @event.AdminUpdateDate(request.Date, utcNow);
            if (updateDateResult.IsFailure)
                return Result.Failure(updateDateResult.Errors.ToArray());

            var updateLocationResult = @event.AdminUpdateLocation(locationResult.Value, utcNow);
            if (updateLocationResult.IsFailure)
                return Result.Failure(updateLocationResult.Errors.ToArray());

            var updateDescriptionResult = @event.AdminUpdateDescription(request.Description, utcNow);
            if (updateDescriptionResult.IsFailure)
                return Result.Failure(updateDescriptionResult.Errors.ToArray());
        }
        else
        {
            var updateNameResult = @event.UpdateName(request.Name, utcNow);
            if (updateNameResult.IsFailure)
                return Result.Failure(updateNameResult.Errors.ToArray());

            var updateCapacityResult = @event.UpdateCapacity(request.Capacity, utcNow);
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
        }

        var rows = await unitOfWork.CommitAsync(cancellationToken);
        if (rows <= 0)
            return Result.Failure(Error.Failure("Event.UpdateFailed", "Failed to update the event."));

        await cache.RemoveAsync($"event:details:{request.EventId}", cancellationToken);
        await cache.RemoveByPatternAsync("events:list:*", cancellationToken);

        return Result.Success();
    }
}
