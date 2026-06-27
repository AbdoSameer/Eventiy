using Domain.Common;
using Domain.Primitives;
using Domain.Aggregates.BookingAggregate.Events;
using Application.Abstractions.Persistence;

namespace Application.EventHandlers
{
    public class BookingCreatedEventHandler : IDomainEventHandler<BookingCreatedEvent>
    {
        private readonly IEventValidator<BookingCreatedEvent> _validator;

        public BookingCreatedEventHandler(IEventValidator<BookingCreatedEvent> validator)
        {
            _validator = validator;
        }

        public async Task<Result> HandleAsync(BookingCreatedEvent @event)
        {
            var validation = await _validator.ValidateAsync(@event);
            if (validation.IsFailure)
                return validation;

            // TODO: implement handling logic (notification, projection update, etc.)

            return Result.Success();
        }
    }
}
