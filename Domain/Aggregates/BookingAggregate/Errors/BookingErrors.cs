
namespace Domain.Aggregates.BookingAggregate.Errors
{
    public static class BookingErrors
    {
        public static string QuantityMustBeGreaterThanZero()
            => "Quantity must be greater than zero.";

        public static string BookingAlreadyCancelled(Guid bookingId)
            => $"BookingAggregate {bookingId} has already been cancelled.";

        public static string BookingExpired(Guid bookingId)
            => $"BookingAggregate {bookingId} has expired.";

        public static string RefundNotAllowed(Guid bookingId)
            => $"Refund not allowed for booking {bookingId}.";

        public static string CannotCancelBooking(Guid bookingId, string reason)
            => $"Cannot cancel booking {bookingId}: {reason}";

        public static string BookingNotPending(Guid value)
            => $"BookingAggregate {value} is not in pending status. Current status: {value}";

        public static string InsufficientSeats(int quantity, int v)
          => $"Insufficient seats. Requested: {quantity}, Available: {v}";
    }
}


//        // Validation Errors
//        public static string InvalidEventId(Guid eventId)
//            => $"Invalid Event ID: {eventId}";

//        public static string InvalidUserId(Guid userId)
//            => $"Invalid User ID: {userId}";

//        public static string InvalidTicketTypeId(Guid ticketTypeId)
//            => $"Invalid Ticket Type ID: {ticketTypeId}";

//        public static string InvalidBookingId(Guid bookingId)
//            => $"Invalid BookingAggregate ID: {bookingId}";

//        public static string InvalidNumberOfTickets(int requestedSeats)
//            => $"Invalid number of tickets requested: {requestedSeats}. Must be greater than 0.";

//        public static string InvalidBookingStatus(string status)
//            => $"Invalid booking status: {status}";

//        public static string InvalidPaymentMethod(string paymentMethod)
//            => $"Invalid payment method: {paymentMethod}";

//        public static string InvalidDiscountCode(string discountCode)
//            => $"Invalid discount code: {discountCode}";

//        public static string InvalidPromoCode(string promoCode)
//            => $"Invalid promo code: {promoCode}";

//        // Availability Errors
//        public static string InsufficientSeats(int requestedSeats, int availableSeats)
//            => $"Insufficient seats. Requested: {requestedSeats}, Available: {availableSeats}";

//        public static string EventFullyBooked(Guid eventId)
//            => $"Event {eventId} is fully booked. No seats available.";

//        public static string TicketTypeSoldOut(Guid ticketTypeId)
//            => $"Ticket type {ticketTypeId} is sold out.";

//        public static string EventNotAvailable(Guid eventId)
//            => $"Event {eventId} is not available for booking.";

//        public static string EventCancelled(Guid eventId)
//            => $"Event {eventId} has been cancelled.";

//        public static string EventPostponed(Guid eventId)
//            => $"Event {eventId} has been postponed.";

//        public static string NoAvailableSeatsForTicketType(Guid ticketTypeId)
//            => $"No available seats for ticket type: {ticketTypeId}";

//        // Time-based Errors
//        public static string BookingWindowExpired(Guid eventId)
//            => $"BookingAggregate window for event {eventId} has expired.";

//        public static string BookingTooEarly(DateTime eventDate, DateTime currentDate)
//            => $"BookingAggregate is too early. Event date: {eventDate}, Current date: {currentDate}";

//        public static string BookingTooLate(DateTime eventDate, DateTime currentDate)
//            => $"BookingAggregate is too late. Event date: {eventDate}, Current date: {currentDate}";

//        public static string EventAlreadyStarted(Guid eventId)
//            => $"Event {eventId} has already started. Cannot book tickets.";

//        public static string EventHasPassed(Guid eventId)
//            => $"Event {eventId} has already passed. Cannot book tickets.";

//        public static string BookingNotAllowedAtThisTime(Guid eventId)
//            => $"BookingAggregate for event {eventId} is not allowed at this time.";

//        // Payment Errors
//        public static string PaymentFailed(string reason)
//            => $"Payment failed: {reason}";

//        public static string PaymentDeclined(string transactionId)
//            => $"Payment declined for transaction: {transactionId}";

//        public static string InsufficientFunds(decimal amount, decimal available)
//            => $"Insufficient funds. Required: {amount:C}, Available: {available:C}";

//        public static string PaymentTimeout(Guid bookingId)
//            => $"Payment timeout for booking: {bookingId}";

//        public static string InvalidPaymentAmount(decimal amount, decimal expected)
//            => $"Invalid payment amount. Expected: {expected:C}, Received: {amount:C}";

//        public static string PaymentAlreadyProcessed(Guid bookingId)
//            => $"Payment for booking {bookingId} has already been processed.";

//        // Cancellation Errors
//        public static string BookingNotFound(Guid bookingId)
//            => $"BookingAggregate not found: {bookingId}";


//        public static string BookingAlreadyConfirmed(Guid bookingId)
//            => $"BookingAggregate {bookingId} has already been confirmed.";

//        public static string CancellationWindowExpired(Guid bookingId)
//            => $"Cancellation window for booking {bookingId} has expired.";


//        public static string BookingNotPending(Guid bookingId)
//            => $"BookingAggregate {bookingId} is not in pending status. Current status: {bookingId}";

//        // Confirmation Errors
//        public static string BookingAlreadyConfirmed(string bookingReference)
//            => $"BookingAggregate {bookingReference} is already confirmed.";


//        public static string CannotConfirmBooking(Guid bookingId, string reason)
//            => $"Cannot confirm booking {bookingId}: {reason}";

//        public static string InvalidConfirmationCode(string confirmationCode)
//            => $"Invalid confirmation code: {confirmationCode}";

//        public static string ConfirmationCodeAlreadyUsed(string confirmationCode)
//            => $"Confirmation code {confirmationCode} has already been used.";

//        // Capacity and Limit Errors
//        public static string ExceedsMaximumTicketsPerBooking(int maxTickets, int requestedTickets)
//            => $"Maximum tickets per booking is {maxTickets}. Requested: {requestedTickets}";

//        public static string UserBookingLimitReached(Guid userId, int limit)
//            => $"User {userId} has reached the maximum booking limit of {limit}.";

//        public static string UserAlreadyBookedForEvent(Guid userId, Guid eventId)
//            => $"User {userId} has already booked for event {eventId}.";

//        // Discount and Promotion Errors
//        public static string DiscountCodeExpired(string discountCode)
//            => $"Discount code {discountCode} has expired.";

//        public static string DiscountCodeNotValid(string discountCode)
//            => $"Discount code {discountCode} is not valid.";

//        public static string DiscountCodeAlreadyUsed(string discountCode)
//            => $"Discount code {discountCode} has already been used.";

//        public static string MinimumPurchaseRequiredForDiscount(decimal minimumAmount, decimal currentAmount)
//            => $"Minimum purchase of {minimumAmount:C} required for discount. Current amount: {currentAmount:C}";

//        // Seat Selection Errors
//        public static string InvalidSeatSelection(string seatSelection)
//            => $"Invalid seat selection: {seatSelection}";

//        public static string SeatAlreadyBooked(string seatId)
//            => $"Seat {seatId} is already booked.";

//        public static string SeatNotAvailable(string seatId)
//            => $"Seat {seatId} is not available.";

//        public static string InvalidSeatCount(int requested, int max)
//            => $"Invalid seat count. Requested: {requested}, Maximum: {max}";

//        // System Errors
//        public static string BookingSystemUnavailable(string reason)
//            => $"BookingAggregate system is currently unavailable: {reason}";

//        public static string ConcurrencyConflict(Guid bookingId)
//            => $"Concurrency conflict detected for booking {bookingId}. Please try again.";

//        public static string DatabaseError(string operation)
//            => $"Database error occurred during {operation} operation.";

//        public static string TimeoutError(string operation)
//            => $"Timeout occurred during {operation} operation.";

//        public static string UnexpectedError(string message)
//            => $"An unexpected error occurred: {message}";

//        // Validation Errors for BookingAggregate Creation
//        public static string MissingRequiredFields(string fields)
//            => $"Missing required fields: {fields}";

//        public static string InvalidEmailFormat(string email)
//            => $"Invalid email format: {email}";

//        public static string InvalidPhoneNumber(string phoneNumber)
//            => $"Invalid phone number: {phoneNumber}";

//        public static string InvalidNameFormat(string name)
//            => $"Invalid name format: {name}";

//        // Guest BookingAggregate Errors
//        public static string GuestBookingNotAllowed(Guid eventId)
//            => $"Guest booking is not allowed for event {eventId}.";

//        public static string MaximumGuestTicketsExceeded(int max, int requested)
//            => $"Maximum guest tickets allowed: {max}. Requested: {requested}";
//    }
//}