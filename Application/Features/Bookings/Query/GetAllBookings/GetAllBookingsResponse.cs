namespace Application.Features.Bookings.Query.GetAllBookings;

public sealed record GetAllBookingsResponse(
    Guid Id,
    Guid EventId,
    Guid UserId,
    string EventTitle,
    string AttendeeName,
    string TicketTypeName,
    int Quantity,
    decimal TotalAmount,
    string Currency,
    string Status,
    string PaymentMethod,
    string? ReferenceCode,
    DateTime BookingDate,
    DateTime? HoldExpiresAt
);
