using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Application.Features.Events.Commands.SetCoverPhoto;

internal sealed class SetCoverPhotoCommandHandler
    : ICommandHandler<SetCoverPhotoCommand>
{
    private readonly IEventRepository _eventRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _dateTimeProvider;
    private readonly IEventMetadataFactory _metadataFactory;

    public SetCoverPhotoCommandHandler(
        IEventRepository eventRepository,
        IUnitOfWork unitOfWork,
        TimeProvider dateTimeProvider,
        IEventMetadataFactory metadataFactory)
    {
        _eventRepository = eventRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _metadataFactory = metadataFactory;
    }

    public async Task<Result> Handle(SetCoverPhotoCommand request, CancellationToken cancellationToken)
    {
        var eventIdResult = EventId.Create(request.EventId);
        if (eventIdResult.IsFailure)
            return Result.Failure(eventIdResult.Errors.ToArray());

        var @event = await _eventRepository.GetByIdAsync(eventIdResult.Value, cancellationToken);
        if (@event is null)
            return Result.Failure(Domain.Errors.EventErrors.EventNotFound(eventIdResult.Value));

        var photoIdResult = EventPhotoId.Create(request.PhotoId);
        if (photoIdResult.IsFailure)
            return Result.Failure(photoIdResult.Errors.ToArray());

        var metadata = _metadataFactory.Create();
        var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;
        var result = @event.SetCoverPhoto(photoIdResult.Value, utcNow, metadata);
        if (result.IsFailure)
            return result;

        await _unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
