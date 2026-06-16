using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;

namespace Application.Features.Events.Queries.GetEventDetails
{
    public class EventDetailsResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty; public DateTime Date { get; set; }
        public string Description { get; init; } = string.Empty;
        public EventStatus Status { get; init; }
        public decimal LowestTicketPrice { get; init; }
        public AddressResponse Location { get; init; } =
            new AddressResponse(string.Empty, string.Empty, string.Empty);
        public List<TicketDetailsResponse> TicketDetails { get; init; }
                                            = new List<TicketDetailsResponse>();

    }
}
