using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
namespace Application.Features.Bookings.Query.GetBookingByEvent
{
    public sealed record GetBookingByEventQuery(
        Guid EventId
        ) : IQuery<List<GetBookingByEventQueryResponse>>;
}
