using Application.Abstractions.Messaging;

namespace Application.Features.Events.Queries.GetEventDetails
{
    public sealed record GetEventDetailsQuery(Guid EventId) : IQuery<EventDetailsResponse>;
}
