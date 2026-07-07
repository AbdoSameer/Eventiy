using Domain.Aggregates.EventAggregate.Enums;

namespace Application.Features.Events.Queries.GetEvents
{
     public record EventCardResponse(
        Guid Id,
        string Title,
        DateTime Date,
        string City,
        string Country,
        decimal LowestPrice,
        string Currency,
        EventStatus Status,
        int TotalSold,
        int TotalCapacity,
        int TicketTypeCount,
        string? Description,
        string? CoverPhotoUrl,
        EventType Type,
        double? Latitude,
        double? Longitude);
}
