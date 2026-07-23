using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.EventAggregate;
using Domain.Common;
using static Application.Abstractions.Caching.CacheKeys;

namespace Application.Features.Events.Queries.GetEvents;

public class GetEventsHandler
    : IQueryHandler<GetEventsQuery, PaginatedEventResponse>
{
    private readonly IApplicationReadDbContext _context;
    private readonly TimeProvider _dateTimeProvider;
    private readonly ICacheService _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public GetEventsHandler(
        IApplicationReadDbContext context,
        TimeProvider dateTimeProvider,
        ICacheService cache)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _cache = cache;
    }

    public async Task<Result<PaginatedEventResponse>> Handle(
        GetEventsQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = EventsList(request.Page, request.PageSize, request.Type?.ToString(), request.UserLatitude, request.UserLongitude, request.DistanceInKm);

        var cached = await _cache.GetAsync<PaginatedEventResponse>(cacheKey, cancellationToken);
        if (cached is not null)
            return Result<PaginatedEventResponse>.Success(cached);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;

        var baseQuery = _context.Query<Event>()
            .Where(e => e.Date > utcNow);

        if (request.Type.HasValue)
            baseQuery = baseQuery.Where(e => e.Type == request.Type.Value);

        if (request.UserLatitude.HasValue && request.UserLongitude.HasValue && request.DistanceInKm > 0)
        {
            var approxDegrees = request.DistanceInKm / 111.0;
            var minLat = request.UserLatitude.Value - approxDegrees;
            var maxLat = request.UserLatitude.Value + approxDegrees;
            var minLng = request.UserLongitude.Value - approxDegrees;
            var maxLng = request.UserLongitude.Value + approxDegrees;
            baseQuery = baseQuery.Where(e =>
                e.Location.Latitude >= minLat &&
                e.Location.Latitude <= maxLat &&
                e.Location.Longitude >= minLng &&
                e.Location.Longitude <= maxLng);
        }

        var totalCount = await _context.CountAsync(baseQuery, cancellationToken);

        // Note: count + list are separate queries (read-model, AsNoTracking).
        // Snapshot isolation level would be needed for strict read consistency.

        var selectQuery = baseQuery
            .OrderBy(e => e.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EventCardResponse(
                e.Id.Value,
                e.EventName.Value,
                e.Date,
                e.Location.City,
                e.Location.Country,
                e.TicketTypes.Any() ? e.TicketTypes.Min(tt => tt.Price.Amount) : 0m,
                e.TicketTypes.OrderBy(tt => tt.Price.Amount)
                    .Select(tt => tt.Price.Currency)
                    .FirstOrDefault() ?? string.Empty,
                e.Status,
                e.TicketTypes.Sum(tt => tt.SoldCount),
                e.Capacity,
                e.TicketTypes.Count,
                e.Description,
                e.Photos
                    .OrderByDescending(p => p.IsCover)
                    .ThenBy(p => p.DisplayOrder)
                    .Select(p => p.PublicUrl)
                    .FirstOrDefault(),
                e.Type,
                e.Location.Latitude,
                e.Location.Longitude,
                e.IsHighDemand
            ));

        var items = await _context.ToListAsync(selectQuery, cancellationToken);

        // Haversine post-filter: bounding box includes corners outside the true radius
        if (request.UserLatitude.HasValue && request.UserLongitude.HasValue && request.DistanceInKm > 0)
        {
            var centerLat = request.UserLatitude.Value * (Math.PI / 180.0);
            var centerLng = request.UserLongitude.Value * (Math.PI / 180.0);
            var maxDistanceKm = request.DistanceInKm;

            items = items.Where(e =>
            {
                if (!e.Latitude.HasValue || !e.Longitude.HasValue)
                    return false;

                var latRad = e.Latitude.Value * (Math.PI / 180.0);
                var lngRad = e.Longitude.Value * (Math.PI / 180.0);

                var dlat = latRad - centerLat;
                var dlng = lngRad - centerLng;
                var a = Math.Sin(dlat / 2) * Math.Sin(dlat / 2) +
                        Math.Cos(centerLat) * Math.Cos(latRad) *
                        Math.Sin(dlng / 2) * Math.Sin(dlng / 2);
                var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
                var distanceKm = 6371 * c;

                return distanceKm <= maxDistanceKm;
            }).ToList();
        }

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var response = new PaginatedEventResponse(
            items, totalCount, page, pageSize, totalPages);

        await _cache.SetAsync(cacheKey, response, CacheTtl, cancellationToken);

        return Result<PaginatedEventResponse>.Success(response);
    }
}