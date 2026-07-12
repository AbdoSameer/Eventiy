namespace Application.Features.Bookings.Query.GetBookingsByUser
{
    public sealed record BookingByUserResponse(
        Guid Id,
        Guid EventId,
        string EventTitle,
        DateTime EventDate,
        string EventCity,
        string TicketTypeName,
        int Quantity,
        decimal TotalAmount,
        string Currency,
        string Status,
        DateTime BookingDate,
        string PaymentMethod,
        string? ReferenceCode,
        DateTime? HoldExpiresAt
    );
}
