using Application.Abstractions.Messaging;

namespace Application.Features.Events.Queries.GetEvents
{
    public sealed record GetEventsQuery : IQuery<List<EventCardResponse>>;

}
