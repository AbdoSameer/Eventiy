using Domain.Common;
using Domain.Aggregates.BookingAggregate.Events;
using Application.Abstractions.Persistence;

namespace Application.Validators;

public class BookingCreatedEventValidator : IEventValidator<BookingCreatedEvent>
{
    public Task<Result> ValidateAsync(
        BookingCreatedEvent @event,
        CancellationToken cancellationToken = default)
    {
        if (@event is null)
            return Task.FromResult(Result.Failure(Error.NullValue));

        if (@event.Quantity <= 0)
            return Task.FromResult(Result.Failure(
                Error.Validation("Booking.QuantityInvalid", "Quantity must be greater than 0")));

        if (@event.TotalAmount <= 0)
            return Task.FromResult(Result.Failure(
                Error.Validation("Booking.TotalAmountInvalid", "Total amount must be greater than 0")));

        return Task.FromResult(Result.Success());
    }
}