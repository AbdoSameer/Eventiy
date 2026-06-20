using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Common;
using Domain.Primitives;
using Domain.Aggregates.BookingAggregate.Errors;

namespace Domain.Aggregates.BookingAggregate
{
    public class Booking : AggregateRoot<BookingId>
    {
        public UserId UserId { get; private set; }
        public EventId EventId { get; private set; }
        public TicketTypeId TicketTypeId { get; private set; }
        public string EventTitle { get; private set; } 
        public int Quantity { get; private set; }
        public DateTime BookingDate { get; private set; }
        public BookingStatusEnum Status { get; private set; }
        public Money Money { get; private set; }
        public decimal TotalAmount  { get; private set; }
        private Booking() : base(default!) { }

        private Booking(BookingId id) : base(id) { }

        private Booking(BookingId id,
                        UserId userId,
                        EventId eventId,
                        TicketTypeId ticketTypeId,
                        string eventTitle,
                        int quantity,
                        Money money) : base(id)
        {
            UserId = userId;
            EventId = eventId;
            TicketTypeId = ticketTypeId;
            EventTitle = eventTitle;
            Quantity = quantity;
            Status = BookingStatusEnum.Pending;
            Money = money;

            TotalAmount = money.Amount * quantity;
            BookingDate = DateTime.UtcNow;
        }

        public static Result<Booking> Create(
            UserId userId,
            EventId eventId,
            TicketTypeId ticketTypeId,
            string EventTitle,
            int quantity,
            Money money)
        {
            if (quantity <= 0)
                return Result<Booking>.Failure(BookingErrors.QuantityMustBeGreaterThanZero());

  
            var bookingId = BookingId.Create(Guid.NewGuid());
            if (bookingId.IsFailure)
                return Result<Booking>.Failure(bookingId.Error);

            var booking = new Booking(bookingId.Value,
                                      userId,
                                      eventId,
                                      ticketTypeId,
                                      EventTitle,
                                      quantity,
                                      money);

            return Result<Booking>.Success(booking);
        }

        public Result Confirm()
        {
            if (Status != BookingStatusEnum.Pending)
                return Result.Failure(BookingErrors.BookingNotPending(Id.Value));
            
            Status = BookingStatusEnum.Confirmed;
            return Result.Success();
        }

        // Cancel booking
        public Result Cancel()
        {
            {
                if (Status == BookingStatusEnum.Cancelled)
                    return Result.Failure(BookingErrors.BookingAlreadyCancelled(Id.Value));

                if (Status == BookingStatusEnum.Expired)
                    return Result.Failure(BookingErrors.BookingExpired(Id.Value));

                if (Status == BookingStatusEnum.Refunded)
                    return Result.Failure(BookingErrors.CannotCancelBooking(Id.Value));

                if (Status == BookingStatusEnum.Confirmed)
                    return Result.Failure(BookingErrors.CannotCancelBooking(Id.Value));

                Status = BookingStatusEnum.Cancelled;
                return Result.Success();
            }
        }

        public Result Refund()
        {
            if (Status != BookingStatusEnum.Cancelled)
                return Result.Failure(BookingErrors.RefundNotAllowed(Id.Value));
            
            Status = BookingStatusEnum.Refunded;
            return Result.Success();
        }

    }
}
