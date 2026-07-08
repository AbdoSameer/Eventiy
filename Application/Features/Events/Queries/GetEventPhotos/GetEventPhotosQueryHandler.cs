using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate.Entities;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;

namespace Application.Features.Events.Queries.GetEventPhotos;

internal sealed class GetEventPhotosQueryHandler
    : IQueryHandler<GetEventPhotosQuery, List<EventPhotoResponse>>
{
    private readonly IApplicationReadDbContext _readDbContext;
    private readonly ICacheService _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    public GetEventPhotosQueryHandler(
        IApplicationReadDbContext readDbContext,
        ICacheService cache)
    {
        _readDbContext = readDbContext;
        _cache = cache;
    }

    public async Task<Result<List<EventPhotoResponse>>> Handle(
        GetEventPhotosQuery request, CancellationToken cancellationToken)
    {
        var eventIdResult = EventId.Create(request.EventId);
        if (eventIdResult.IsFailure)
            return Result<List<EventPhotoResponse>>.Failure(eventIdResult.Errors.ToArray());

        // Cache-Aside: photos are nearly static, so we cache them longer than event details.
        var cacheKey = $"event:photos:{request.EventId}";

        var cached = await _cache.GetAsync<List<EventPhotoResponse>>(cacheKey, cancellationToken);
        if (cached is not null)
            return Result<List<EventPhotoResponse>>.Success(cached);

        var query = _readDbContext.Query<EventPhoto>()
            .Where(p => p.EventId == eventIdResult.Value)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new EventPhotoResponse(
                p.Id.Value,
                p.PublicUrl,
                p.Caption,
                p.DisplayOrder,
                p.IsCover,
                p.UploadedAt));

        var photos = await _readDbContext.ToListAsync(query, cancellationToken);

        await _cache.SetAsync(cacheKey, photos, CacheTtl, cancellationToken);

        return Result<List<EventPhotoResponse>>.Success(photos);
    }
}
