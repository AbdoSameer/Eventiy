using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.BookingAggregate.Events;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Primitives;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.UserAggregate.ValueObject;
using System.Security.Cryptography;

namespace Domain.Aggregates.BookingAggregate
{
    public class Booking : AggregateRoot<BookingId>
    {
        private const int MAX_QUANTITY_PER_BOOKING = 10;
        private const int REFUND_PERIOD_DAYS = 7;

        public UserId UserId { get; private set; }
        public EventId EventId { get; private set; }
        public TicketTypeId TicketTypeId { get; private set; }
        public string EventTitle { get; private set; }
        public int Quantity { get; private set; }
        public DateTime BookingDate { get; private set; }
        public BookingStatusEnum Status { get; private set; }
        public Money Money { get; private set; }
        public decimal TotalAmount { get; private set; }
        public DateTime? HoldExpiresAt { get; private set; }
        public DateTime? ConfirmationDate { get; private set; }
        public DateTime? CancellationDate { get; private set; }
        public DateTime? RefundDate { get; private set; }
        public string? CancellationReason { get; private set; }
        public PaymentMethod PaymentMethod { get; private set; }
        public string? ReferenceCode { get; private set; }

        private Booking() : base(default!) { }

        private Booking(BookingId id) : base(id) { }

        private Booking(
            BookingId id,
            UserId userId,
            EventId eventId,
            TicketTypeId ticketTypeId,
            string eventTitle,
            int quantity,
            Money money,
            PaymentMethod paymentMethod,        
            DateTime utcNow) : base(id)
        {
            UserId = userId;
            EventId = eventId;
            TicketTypeId = ticketTypeId;
            EventTitle = eventTitle;
            Quantity = quantity;
            Status = BookingStatusEnum.Pending;
            Money = money;
            TotalAmount = money.Amount * quantity;
            BookingDate = utcNow;
            PaymentMethod = paymentMethod;

            if (paymentMethod == PaymentMethod.Deferred)
            {
                ReferenceCode = GenerateReferenceCode();
                HoldExpiresAt = utcNow.AddMinutes(30);
            }
            else
            {
                HoldExpiresAt = utcNow.AddMinutes(2);
            }
        }

        private static string GenerateReferenceCode()
        {
            Span<byte> bytes = stackalloc byte[4];
            RandomNumberGenerator.Fill(bytes);
            return $"FAW-{Convert.ToHexString(bytes)}";
        }

        public static Result<Booking> Create(
            UserId userId,
            EventId eventId,
            TicketTypeId ticketTypeId,
            string eventTitle,
            int quantity,
            Money money,
            PaymentMethod paymentMethod,       
            DateTime utcNow)
        {
            // Validate quantity
            if (quantity <= 0)
                return Result<Booking>.Failure(BookingErrors.QuantityMustBeGreaterThanZero());
            if (quantity > MAX_QUANTITY_PER_BOOKING)
                return Result<Booking>.Failure(BookingErrors.MaximumQuantityExceeded(MAX_QUANTITY_PER_BOOKING));
            // Validate event title
            if (string.IsNullOrWhiteSpace(eventTitle))
                return Result<Booking>.Failure(BookingErrors.EventTitleCannotBeEmpty());
            // Validate money
            if (money is null || money.Amount <= 0)
                return Result<Booking>.Failure(BookingErrors.InvalidMoneyAmount());
            // Create booking ID
            var bookingId = BookingId.Create(Guid.NewGuid());
            if (bookingId.IsFailure)
                return Result<Booking>.Failure(bookingId.Errors.ToArray());

            var booking = new Booking(
                bookingId.Value,
                userId,
                eventId,
                ticketTypeId,
                eventTitle,
                quantity,
                money,
                paymentMethod,         
                utcNow);

            booking.RaiseDomainEvent(new BookingCreatedEvent(
                booking.Id,
                booking.UserId,
                booking.EventId,
                booking.TicketTypeId,   
                booking.Quantity,
                booking.TotalAmount,
                utcNow));

            return Result<Booking>.Success(booking);
        }

        public Result Confirm(DateTime utcNow)
        {
            if (Status == BookingStatusEnum.Confirmed)
                return Result.Failure(BookingErrors.BookingAlreadyConfirmed(Id.Value));
          
            if (Status == BookingStatusEnum.Expired)
                return Result.Failure(BookingErrors.BookingExpired(Id.Value));
           
            if (Status == BookingStatusEnum.Cancelled)
                return Result.Failure(BookingErrors.BookingAlreadyCancelled(Id.Value));
            
            if (Status == BookingStatusEnum.Refunded)
                return Result.Failure(BookingErrors.CannotModifyRefundedBooking(Id.Value));
        
            if (Status != BookingStatusEnum.Pending)  
                return Result.Failure(BookingErrors.BookingNotPending(Id.Value, Status));

            Status = BookingStatusEnum.Confirmed;
            ConfirmationDate = utcNow;
            
            RaiseDomainEvent(new BookingConfirmedEvent(Id, UserId, EventId, TicketTypeId, Quantity, utcNow));
          
            return Result.Success();
        }

        public Result Cancel(DateTime utcNow, string? reason = null)
        {
            if (Status != BookingStatusEnum.Pending && Status != BookingStatusEnum.Confirmed)
                return Result.Failure(BookingErrors.CannotCancelBooking(Id.Value, Status));

            if (Status == BookingStatusEnum.Cancelled)
                return Result.Failure(BookingErrors.BookingAlreadyCancelled(Id.Value));

            if (Status == BookingStatusEnum.Expired)
                return Result.Failure(BookingErrors.BookingExpired(Id.Value));

            if (Status == BookingStatusEnum.Refunded)
                return Result.Failure(BookingErrors.CannotModifyRefundedBooking(Id.Value));

            Status = BookingStatusEnum.Cancelled;
            CancellationDate = utcNow;
            CancellationReason = reason;

            RaiseDomainEvent(new BookingCancelledEvent(Id, UserId, EventId, TicketTypeId, Quantity, utcNow, reason));

            return Result.Success();
        }

        public Result RequestCancellation(DateTime utcNow, string? reason = null)
        {
            if (Status == BookingStatusEnum.Cancelled)
                return Result.Failure(BookingErrors.BookingAlreadyCancelled(Id.Value));

            if (Status == BookingStatusEnum.Expired)
                return Result.Failure(BookingErrors.BookingExpired(Id.Value));

            if (Status == BookingStatusEnum.Refunded)
                return Result.Failure(BookingErrors.CannotModifyRefundedBooking(Id.Value));

            // Can request cancellation for confirmed bookings
            if (Status != BookingStatusEnum.Confirmed && Status != BookingStatusEnum.Pending)
                return Result.Failure(BookingErrors.CannotCancelBooking(Id.Value, Status));

            // For confirmed bookings, request cancellation (admin approval needed)
            if (Status == BookingStatusEnum.Confirmed)
            {
                RaiseDomainEvent(new BookingCancellationRequestedEvent(Id, UserId, EventId, utcNow, reason));
                return Result.Success();
            }

            // For pending bookings, cancel directly
            return Cancel(utcNow, reason);
        }

        public Result Refund(DateTime utcNow)
        {
            if (Status != BookingStatusEnum.Cancelled)
                return Result.Failure(BookingErrors.RefundNotAllowed(Id.Value));

            if (Status == BookingStatusEnum.Refunded)
                return Result.Failure(BookingErrors.BookingAlreadyRefunded(Id.Value));

            // Check if refund period has expired (e.g., 7 days from cancellation)
            if (CancellationDate.HasValue)
            {
                var refundDeadline = CancellationDate.Value.AddDays(REFUND_PERIOD_DAYS);
                if (utcNow > refundDeadline)
                    return Result.Failure(BookingErrors.RefundPeriodExpired(Id.Value, refundDeadline));
            }

            Status = BookingStatusEnum.Refunded;
            RefundDate = utcNow;

            RaiseDomainEvent(new BookingRefundedEvent(Id, UserId, EventId, TotalAmount, utcNow));

            return Result.Success();
        }

        public Result Expire(DateTime utcNow)
        {
            if (Status != BookingStatusEnum.Pending)
                return Result.Failure(BookingErrors.CannotExpireBooking(Id.Value, Status));

            if (HoldExpiresAt is null || utcNow < HoldExpiresAt.Value)
                return Result.Failure(BookingErrors.HoldNotYetExpired(Id.Value));

            Status = BookingStatusEnum.Expired;

            RaiseDomainEvent(new BookingExpiredEvent(
                Id, UserId, EventId, TicketTypeId, Quantity, utcNow));

            return Result.Success();
        }

        public Result UpdateQuantity(int newQuantity, DateTime utcNow)
        {
            if (Status != BookingStatusEnum.Pending)
                return Result.Failure(BookingErrors.BookingNotPending(Id.Value, Status));

            if (newQuantity <= 0)
                return Result.Failure(BookingErrors.QuantityMustBeGreaterThanZero());

            if (newQuantity > MAX_QUANTITY_PER_BOOKING)
                return Result.Failure(BookingErrors.MaximumQuantityExceeded(MAX_QUANTITY_PER_BOOKING));

            // Recalculate total amount
            var oldTotal = TotalAmount;
            Quantity = newQuantity;
            TotalAmount = Money.Amount * newQuantity;

            RaiseDomainEvent(new BookingQuantityUpdatedEvent(Id, oldTotal, TotalAmount, utcNow));

            return Result.Success();
        }

        // Helper method to check if booking can be modified
        public bool CanBeModified()
        {
            return Status == BookingStatusEnum.Pending;
        }

        // Helper method to check if booking is active
        public bool IsActive()
        {
            return Status == BookingStatusEnum.Pending || Status == BookingStatusEnum.Confirmed;
        }
    }
}
