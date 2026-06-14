namespace Application.Features.Events.Queries.GetEventDetails
{
    internal class EventDetailsResponse
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public AddressResponse Location { get; set; } = new AddressResponse();
        public List<TicketDetailsResponse> TicketDetails { get; set; }
                                            = new List<TicketDetailsResponse>();

    }
}

