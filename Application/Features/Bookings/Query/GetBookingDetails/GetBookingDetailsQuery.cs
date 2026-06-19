using Application.Abstractions.Messaging;


namespace Application.Features.Bookings.Query.GetBookingDetails
{
    public sealed record GetBookingDetailsQuery(Guid BookingId) : IQuery<GetBookingDetailsResponse>;
    
}
