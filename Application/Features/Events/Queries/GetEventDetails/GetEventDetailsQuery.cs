
using Application.Abstractions.Messaging;
using Domain.Aggregates.EventAggregate.ValueObject;

namespace Application.Features.Events.Queries.GetEventDetails
{
    public sealed record GetEventDetailsQuery
    (Guid Id) : IQuery<EventDetailsResponse>;

}
