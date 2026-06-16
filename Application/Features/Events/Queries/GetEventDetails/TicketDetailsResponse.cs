namespace Application.Features.Events.Queries.GetEventDetails
{
    public class TicketDetailsResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Currency { get; set; } = string.Empty;
        public int Capacity { get; set; }

    }

}

