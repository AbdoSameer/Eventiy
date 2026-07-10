using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Application.Features.Events.Commands.UpdatePhotoMetadata;

internal sealed class UpdatePhotoMetadataCommandHandler
    : ICommandHandler<UpdatePhotoMetadataCommand>
{
    private readonly IEventRepository _eventRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _dateTimeProvider;
    private readonly ICacheService _cache;

    public UpdatePhotoMetadataCommandHandler(
        IEventRepository eventRepository,
        IUnitOfWork unitOfWork,
        TimeProvider dateTimeProvider,
        ICacheService cache)
    {
        _eventRepository = eventRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _cache = cache;
    }

    public async Task<Result> Handle(UpdatePhotoMetadataCommand request, CancellationToken cancellationToken)
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

        if (request.Caption is not null)
        {
            var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;
            var captionResult = @event.UpdatePhotoCaption(photoIdResult.Value, request.Caption, utcNow);
            if (captionResult.IsFailure)
                return captionResult;
        }

        if (request.DisplayOrder.HasValue)
        {
            var photo = @event.Photos.FirstOrDefault(p => p.Id == photoIdResult.Value);
            if (photo is null)
                return Result.Failure(Domain.Errors.EventErrors.PhotoNotFound(photoIdResult.Value));

            var orderResult = photo.UpdateDisplayOrder(request.DisplayOrder.Value);
            if (orderResult.IsFailure)
                return orderResult;
        }

        await _unitOfWork.CommitAsync(cancellationToken);

        // Invalidate the photo list cache (caption/display-order changed).
        await _cache.RemoveAsync($"event:photos:{request.EventId}", cancellationToken);

        return Result.Success();
    }
}
