using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Features.Events.Queries.GetEventPhotos;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;

namespace Application.Features.Events.Queries.GetEventDetails;

public class GetEventDetailsHandler
    : IQueryHandler<GetEventDetailsQuery, EventDetailsResponse>
{
    private readonly IApplicationReadDbContext _context;
    private readonly ICacheService _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public GetEventDetailsHandler(
        IApplicationReadDbContext context,
        ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<Result<EventDetailsResponse>> Handle(
        GetEventDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"event:details:{request.Id}";

        var cached = await _cache.GetAsync<EventDetailsResponse>(cacheKey, cancellationToken);
        if (cached is not null)
            return Result<EventDetailsResponse>.Success(cached);

        var eventIdResult = EventId.Create(request.Id);
        if (eventIdResult.IsFailure)
            return Result<EventDetailsResponse>.Failure(
                eventIdResult.Errors.ToArray());

        var query = _context.Query<Event>()
            .Where(e => e.Id == eventIdResult.Value)
            .Select(e => new EventDetailsResponse
            {
                Id = e.Id.Value,
                Date = e.Date,
                Name = e.EventName.Value,
                Description = e.Description,
                Status = e.Status,
                Type = e.Type,
                LowestTicketPrice = e.TicketTypes
                    .Min(t => (decimal?)t.Price.Amount) ?? 0m,
                TotalSold = e.TicketTypes.Sum(t => t.SoldCount),
                Location = new AddressResponse(
                    e.Location.Country,
                    e.Location.City,
                    e.Location.Street,
                    e.Location.Latitude,
                    e.Location.Longitude),
                TicketDetails = e.TicketTypes
                    .Select(t => new TicketDetailsResponse(
                        t.Id.Value,
                        t.Price.Amount,
                        t.Price.Currency,
                        t.TicketTypeName,
                        t.Capacity))
                    .ToList(),
                Photos = e.Photos
                    .OrderBy(p => p.DisplayOrder)
                    .Select(p => new EventPhotoResponse(
                        p.Id.Value,
                        p.PublicUrl,
                        p.Caption,
                        p.DisplayOrder,
                        p.IsCover,
                        p.UploadedAt))
                    .ToList(),
                CoverPhotoUrl = e.Photos
                    .OrderByDescending(p => p.IsCover)
                    .ThenBy(p => p.DisplayOrder)
                    .Select(p => p.PublicUrl)
                    .FirstOrDefault()
            });

        var result = await _context.FirstOrDefaultAsync(query, cancellationToken);

        if (result is null)
            return Result<EventDetailsResponse>.Failure(
                EventErrors.NotFound(request.Id));

        await _cache.SetAsync(cacheKey, result, CacheTtl, cancellationToken);

        return Result<EventDetailsResponse>.Success(result);
    }
}
