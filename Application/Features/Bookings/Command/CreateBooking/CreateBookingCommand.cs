using Application.Abstractions.Messaging;
using Domain.Aggregates.BookingAggregate.ValueObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Bookings.Command.CreateBooking
{
    public sealed record CreateBookingCommand : ICommand<BookingId>
    {
        public Guid EventId { get; init; }
        //public Guid UserId { get; init; }
        public Guid TicketTypeId { get; init; }
        public int Quantity { get; init; }

    }
}
