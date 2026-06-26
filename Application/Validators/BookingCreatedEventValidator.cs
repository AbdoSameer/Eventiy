using Domain.Common;
using Domain.Primitives;
using Domain.Aggregates.BookingAggregate.Events;

namespace Application.Validators
{
    public class BookingCreatedEventValidator : IEventValidator<BookingCreatedEvent>
    {
        public Result Validate(BookingCreatedEvent @event)
        {
            if (@event == null)
                return Result.Failure(Error.NullValue);

            if (@event.Quantity <= 0)
                return Result.Failure(Error.Validation("Booking.QuantityInvalid", "Quantity must be greater than 0"));

            if (@event.TotalAmount <= 0)
                return Result.Failure(Error.Validation("Booking.TotalAmountInvalid", "Total amount must be greater than 0"));

            return Result.Success();
        }
    }
}
