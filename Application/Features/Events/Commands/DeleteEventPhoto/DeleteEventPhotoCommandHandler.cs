using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Storage;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Persistence.Repositories;

namespace Application.Features.Events.Commands.DeleteEventPhoto;

internal sealed class DeleteEventPhotoCommandHandler
    : ICommandHandler<DeleteEventPhotoCommand>
{
    private readonly IEventRepository _eventRepository;
    private readonly IFileStorageService _storageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _dateTimeProvider;
    private readonly IEventMetadataFactory _metadataFactory;

    public DeleteEventPhotoCommandHandler(
        IEventRepository eventRepository,
        IFileStorageService storageService,
        IUnitOfWork unitOfWork,
        TimeProvider dateTimeProvider,
        IEventMetadataFactory metadataFactory)
    {
        _eventRepository = eventRepository;
        _storageService = storageService;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _metadataFactory = metadataFactory;
    }

    public async Task<Result> Handle(DeleteEventPhotoCommand request, CancellationToken cancellationToken)
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

        var photo = @event.Photos.FirstOrDefault(p => p.Id == photoIdResult.Value);
        if (photo is null)
            return Result.Failure(Domain.Errors.EventErrors.PhotoNotFound(photoIdResult.Value));

        var metadata = _metadataFactory.Create();
        var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;
        var removeResult = @event.RemovePhoto(photoIdResult.Value, utcNow, metadata);
        if (removeResult.IsFailure)
            return removeResult;

        var deleteResult = await _storageService.DeleteAsync(photo.StoragePath, cancellationToken);
        if (deleteResult.IsFailure)
            return deleteResult;

        await _unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
