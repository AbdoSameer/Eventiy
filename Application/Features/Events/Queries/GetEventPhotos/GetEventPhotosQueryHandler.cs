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

    public GetEventPhotosQueryHandler(IApplicationReadDbContext readDbContext)
    {
        _readDbContext = readDbContext;
    }

    public async Task<Result<List<EventPhotoResponse>>> Handle(
        GetEventPhotosQuery request, CancellationToken cancellationToken)
    {
        var eventIdResult = EventId.Create(request.EventId);
        if (eventIdResult.IsFailure)
            return Result<List<EventPhotoResponse>>.Failure(eventIdResult.Errors.ToArray());

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

        return Result<List<EventPhotoResponse>>.Success(photos);
    }
}
