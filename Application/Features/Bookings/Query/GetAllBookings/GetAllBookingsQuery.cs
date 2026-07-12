using Application.Abstractions.Messaging;

namespace Application.Features.Bookings.Query.GetAllBookings;

public sealed record GetAllBookingsQuery(
    string? Status = null
) : IQuery<List<GetAllBookingsResponse>>;
