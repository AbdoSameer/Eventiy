using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Primitives;

namespace Application.Features.Events.Commands.PublishEvent;

public sealed class PublishEventCommandHandler(
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    TimeProvider dateTimeProvider,
    ICacheService cache) : ICommandHandler<PublishEventCommand>
{
    public async Task<Result> Handle(PublishEventCommand request, CancellationToken cancellationToken)
    {
        var eventIdResult = EventId.Create(request.EventId);
        if (eventIdResult.IsFailure)
            return Result.Failure(eventIdResult.Errors.ToArray());

        var @event = await eventRepository.GetByIdAsync(eventIdResult.Value, cancellationToken);
        if (@event is null)
            return Result.Failure(EventErrors.EventNotFound(eventIdResult.Value));

        var utcNow = dateTimeProvider.GetUtcNow().UtcDateTime;

        var publishResult = @event.Publish(utcNow);
        if (publishResult.IsFailure)
            return publishResult;

        var rows = await unitOfWork.CommitAsync(cancellationToken);
        if (rows <= 0)
            return Result.Failure(Error.Failure("Event.PublishFailed", "Failed to publish the event."));

        await cache.RemoveAsync($"event:details:{request.EventId}", cancellationToken);
        await cache.RemoveByPatternAsync("events:list:*", cancellationToken);

        return Result.Success();
    }
}
