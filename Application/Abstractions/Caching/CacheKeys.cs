using System.Globalization;

namespace Application.Abstractions.Caching;

public static class CacheKeys
{
    public static string EventDetails(string eventId) => $"event:details:{eventId}";

    public static string EventDetails(Guid eventId) => $"event:details:{eventId}";

    public static string EventsList(int page, int pageSize, string? type, double? lat, double? lng, double dist)
    {
        var latStr = lat?.ToString("F5", CultureInfo.InvariantCulture) ?? "null";
        var lngStr = lng?.ToString("F5", CultureInfo.InvariantCulture) ?? "null";
        var distStr = dist.ToString("F2", CultureInfo.InvariantCulture);
        return $"events:list:{page}:{pageSize}:{type}:{latStr}:{lngStr}:{distStr}";
    }

    public static string EventsListPattern => "events:list:*";

    public static string EventPhotos(Guid eventId) => $"event:photos:{eventId}";
}
