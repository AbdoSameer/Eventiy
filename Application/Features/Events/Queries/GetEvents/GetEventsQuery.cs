using Application.Abstractions.Messaging;
using Domain.Aggregates.EventAggregate.Enums;

namespace Application.Features.Events.Queries.GetEvents
{
    public sealed record GetEventsQuery(
        int Page = 1,
        int PageSize = 20,
        EventType? Type = null,
        double? UserLatitude = null,
        double? UserLongitude = null,
        double DistanceInKm = 20) : IQuery<PaginatedEventResponse>;

}
