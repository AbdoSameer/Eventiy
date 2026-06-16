using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;

namespace Application.Features.Events.Queries.GetEventDetails
{
    public class EventDetailsResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty; public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public EventStatus Status { get; set; }
        public decimal LowestTicketPrice { get; set; }
        public AddressResponse Location { get; set; } = new AddressResponse();
        public List<TicketDetailsResponse> TicketDetails { get; set; }
                                            = new List<TicketDetailsResponse>();

    }
}
