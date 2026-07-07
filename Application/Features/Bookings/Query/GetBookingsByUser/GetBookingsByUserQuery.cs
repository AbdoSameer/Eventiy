using Application.Abstractions.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Bookings.Query.GetBookingsByUser
{
    public sealed record GetBookingsByUserQuery : IQuery<List<BookingByUserResponse>>;
}
