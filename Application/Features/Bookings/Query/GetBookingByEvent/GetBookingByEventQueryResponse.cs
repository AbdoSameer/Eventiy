namespace Application.Features.Bookings.Query.GetBookingByEvent
{
    public sealed record GetBookingByEventQueryResponse(
        Guid Id,
        Guid EventId,
        Guid UserId,
        DateTime BookingDate,
        int Quantity,
        decimal TotalAmount
        );
    
}
