using Domain.Common;
using Domain.Primitives;
using Domain.Aggregates.BookingAggregate.Events;
using Application.Abstractions.Persistence;

namespace Application.Validators
{
    public class BookingCreatedEventValidator : IEventValidator<BookingCreatedEvent>
    {
        public async Task<Result> ValidateAsync(BookingCreatedEvent @event)
        {
            if (@event == null)
                return Result.Failure(Error.NullValue);

            if (@event.Quantity <= 0)
                return Result.Failure(Error.Validation("Booking.QuantityInvalid", "Quantity must be greater than 0"));

            if (@event.TotalAmount <= 0)
                return Result.Failure(Error.Validation("Booking.TotalAmountInvalid", "Total amount must be greater than 0"));

            // Future point: async validation can be added here (e.g., Redis cache check, database query)
            // await _redisCache.GetAsync(...)
            // await _database.QueryAsync(...)

            return await Task.FromResult(Result.Success());
        }
    }
}
