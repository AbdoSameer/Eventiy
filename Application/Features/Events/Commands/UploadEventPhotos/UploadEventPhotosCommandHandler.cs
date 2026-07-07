using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Features.Events.Queries.GetEventPhotos;
using Domain.Abstractions.Persistence;
using Domain.Abstractions.Storage;
using Domain.Aggregates.EventAggregate.Entities;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Persistence.Repositories;

namespace Application.Features.Events.Commands.UploadEventPhotos;

internal sealed class UploadEventPhotosCommandHandler
    : ICommandHandler<UploadEventPhotosCommand, List<EventPhotoResponse>>
{
    private readonly IEventRepository _eventRepository;
    private readonly IEventPhotoRepository _photoRepository;
    private readonly IFileStorageService _storageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _dateTimeProvider;
    private readonly IEventMetadataFactory _metadataFactory;

    public UploadEventPhotosCommandHandler(
        IEventRepository eventRepository,
        IEventPhotoRepository photoRepository,
        IFileStorageService storageService,
        IUnitOfWork unitOfWork,
        TimeProvider dateTimeProvider,
        IEventMetadataFactory metadataFactory)
    {
        _eventRepository = eventRepository;
        _photoRepository = photoRepository;
        _storageService = storageService;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _metadataFactory = metadataFactory;
    }

    public async Task<Result<List<EventPhotoResponse>>> Handle(
        UploadEventPhotosCommand request, CancellationToken cancellationToken)
    {
        var eventIdResult = EventId.Create(request.EventId);
        if (eventIdResult.IsFailure)
            return Result<List<EventPhotoResponse>>.Failure(eventIdResult.Errors.ToArray());

        var @event = await _eventRepository.GetByIdAsync(eventIdResult.Value, cancellationToken);
        if (@event is null)
            return Result<List<EventPhotoResponse>>.Failure(
                Domain.Errors.EventErrors.EventNotFound(eventIdResult.Value));

        var metadata = _metadataFactory.Create();
        var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;
        var uploadedPaths = new List<string>();
        var responses = new List<EventPhotoResponse>();
        var displayOrder = @event.Photos.Count;

        try
        {
            foreach (var file in request.Photos)
            {
                using var fileStream = new MemoryStream(file.Content);
                var uploadResult = await _storageService.UploadAsync(
                    fileStream, file.FileName, file.ContentType, cancellationToken);

                if (uploadResult.IsFailure)
                {
                    foreach (var path in uploadedPaths)
                        await _storageService.DeleteAsync(path, cancellationToken);

                    return Result<List<EventPhotoResponse>>.Failure(uploadResult.Errors.ToArray());
                }

                var storagePath = uploadResult.Value;
                uploadedPaths.Add(storagePath);

                var publicUrl = _storageService.GetPublicUrl(storagePath);

                var photoResult = EventPhoto.Create(
                    eventIdResult.Value,
                    file.FileName,
                    storagePath,
                    publicUrl,
                    displayOrder++,
                    utcNow);

                if (photoResult.IsFailure)
                {
                    foreach (var path in uploadedPaths)
                        await _storageService.DeleteAsync(path, cancellationToken);

                    return Result<List<EventPhotoResponse>>.Failure(photoResult.Errors.ToArray());
                }

                var photo = photoResult.Value;

                var addResult = @event.AddPhoto(photo, utcNow, metadata);
                if (addResult.IsFailure)
                {
                    foreach (var path in uploadedPaths)
                        await _storageService.DeleteAsync(path, cancellationToken);

                    return Result<List<EventPhotoResponse>>.Failure(addResult.Errors.ToArray());
                }

                _photoRepository.Add(photo);
                responses.Add(new EventPhotoResponse(
                    photo.Id.Value,
                    photo.PublicUrl,
                    photo.Caption,
                    photo.DisplayOrder,
                    photo.IsCover,
                    photo.UploadedAt));
            }

            await _unitOfWork.CommitAsync(cancellationToken);

            return Result<List<EventPhotoResponse>>.Success(responses);
        }
        catch (OperationCanceledException)
        {
            foreach (var path in uploadedPaths)
                await _storageService.DeleteAsync(path, CancellationToken.None);

            return Result<List<EventPhotoResponse>>.Failure(
                Error.Failure("Upload.Cancelled", "Upload was cancelled."));
        }
        catch (Exception ex)
        {
            foreach (var path in uploadedPaths)
                await _storageService.DeleteAsync(path, CancellationToken.None);

            return Result<List<EventPhotoResponse>>.Failure(
                Error.Failure("Upload.Failed", $"Upload failed: {ex.Message}"));
        }
    }
}
