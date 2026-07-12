namespace Application.Features.Events.Queries.GetEventDetails
{

    public record TicketDetailsResponse(
        Guid Id,
        decimal Price,
        string Currency,
        string name,
        int Capacity,
        string? SectionCode = null,
        string? VenueType = null);

}

