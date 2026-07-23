using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using static Application.Abstractions.Caching.CacheKeys;

namespace Application.Features.Events.Commands.ReorderEventPhotos;

internal sealed class ReorderEventPhotosCommandHandler
    : ICommandHandler<ReorderEventPhotosCommand>
{
    private readonly IEventRepository _eventRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _dateTimeProvider;
    private readonly ICacheService _cache;

    public ReorderEventPhotosCommandHandler(
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

    public async Task<Result> Handle(ReorderEventPhotosCommand request, CancellationToken cancellationToken)
    {
        var eventIdResult = EventId.Create(request.EventId);
        if (eventIdResult.IsFailure)
            return Result.Failure(eventIdResult.Errors.ToArray());

        var @event = await _eventRepository.GetByIdAsync(eventIdResult.Value, cancellationToken);
        if (@event is null)
            return Result.Failure(Domain.Errors.EventErrors.EventNotFound(eventIdResult.Value));

        var orderedIds = request.OrderedPhotoIds
            .Select(id => EventPhotoId.Create(id))
            .ToList();

        if (orderedIds.Any(r => r.IsFailure))
        {
            var errors = orderedIds.Where(r => r.IsFailure).SelectMany(r => r.Errors).ToArray();
            return Result.Failure(errors);
        }

        var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;
        var result = @event.ReorderPhotos(
            orderedIds.Select(r => r.Value).ToList(),
            utcNow);

        if (result.IsFailure)
            return result;

        await _unitOfWork.CommitAsync(cancellationToken);

        // Invalidate the photo list cache so the new order reflects on next read.
        await _cache.RemoveAsync(EventPhotos(request.EventId), cancellationToken);

        return Result.Success();
    }
}
