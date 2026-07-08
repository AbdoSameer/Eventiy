using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;

namespace Application.Features.Events.Commands.CancelEvent;

public sealed class CancelEventCommandHandler(
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    TimeProvider dateTimeProvider,
    IEventMetadataFactory metadataFactory) : ICommandHandler<CancelEventCommand>
{
    public async Task<Result> Handle(CancelEventCommand request, CancellationToken cancellationToken)
    {
        var eventIdResult = EventId.Create(request.EventId);
        if (eventIdResult.IsFailure)
            return Result.Failure(eventIdResult.Errors.ToArray());

        var @event = await eventRepository.GetByIdAsync(eventIdResult.Value, cancellationToken);
        if (@event is null)
            return Result.Failure(EventErrors.EventNotFound(eventIdResult.Value));

        var metadata = metadataFactory.Create();
        var utcNow = dateTimeProvider.GetUtcNow().UtcDateTime;
        var cancelResult = @event.Cancel(utcNow, metadata);
        if (cancelResult.IsFailure)
            return Result.Failure(cancelResult.Errors.ToArray());

        var rows = await unitOfWork.CommitAsync(cancellationToken);
        if (rows <= 0)
            return Result.Failure(Error.Failure("Event.CancelFailed", "Failed to cancel the event."));

        return Result.Success();
    }
}
