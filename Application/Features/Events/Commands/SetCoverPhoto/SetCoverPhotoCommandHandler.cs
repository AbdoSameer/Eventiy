using Application.Abstractions.Caching;
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
    private readonly ICacheService _cache;

    public SetCoverPhotoCommandHandler(
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

        var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;
        var result = @event.SetCoverPhoto(photoIdResult.Value, utcNow);
        if (result.IsFailure)
            return result;

        await _unitOfWork.CommitAsync(cancellationToken);

        // Invalidate the photo list cache (IsCover flag changed) + event details (CoverPhotoUrl changed).
        await _cache.RemoveAsync($"event:photos:{request.EventId}", cancellationToken);
        await _cache.RemoveAsync($"event:details:{request.EventId}", cancellationToken);

        return Result.Success();
    }
}
