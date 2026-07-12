namespace Application.Features.Bookings.Query.GetBookingDetails
{
    public sealed record GetBookingDetailsResponse(
        Guid Id,
        Guid EventId,
        Guid UserId,
        Guid TicketTypeId,
        string EventTitle,
        int Quantity,
        DateTime BookingDate,
        string Status,
        decimal TotalAmount,
        string Currency,
        string PaymentMethod,
        string? ReferenceCode,
        DateTime? HoldExpiresAt
    );
}
