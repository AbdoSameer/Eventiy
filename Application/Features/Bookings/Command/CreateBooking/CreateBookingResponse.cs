namespace Application.Features.Bookings.Command.CreateBooking
{
    public sealed record CreateBookingResponse(
        Guid BookingId,
        string? PaymentUrl,
        string? ClientSecret);
}
