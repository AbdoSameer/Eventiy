using Application.Features.Events.Queries.GetEventPhotos;
using Domain.Aggregates.EventAggregate.Enums;
// EventStatus already imported above

namespace Application.Features.Events.Queries.GetEventDetails
{
    public class EventDetailsResponse
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public DateTime Date { get; set; }
        public string Description { get; init; } = string.Empty;
        public EventStatus Status { get; init; }
        public EventType Type { get; init; }
        public int Capacity { get; init; }
        public decimal LowestTicketPrice { get; init; }
        public int TotalSold { get; init; }
        public AddressResponse Location { get; init; }
        public List<TicketDetailsResponse> TicketDetails { get; init; }
                                            = new List<TicketDetailsResponse>();
        public List<EventPhotoResponse> Photos { get; init; }
                                            = new List<EventPhotoResponse>();
        public string? CoverPhotoUrl { get; init; }
        public bool IsHighDemand { get; init; }
    }
}

