using Domain.Common;
using Domain.Primitives;
using Domain.Aggregates.BookingAggregate.Events;

namespace Application.EventHandlers
{
    public class BookingCreatedEventHandler : IDomainEventHandler<BookingCreatedEvent>
    {
        private readonly IEventValidator<BookingCreatedEvent> _validator;

        public BookingCreatedEventHandler(IEventValidator<BookingCreatedEvent> validator)
        {
            _validator = validator;
        }

        public Result Handle(BookingCreatedEvent @event)
        {
            var validation = _validator.Validate(@event);
            if (validation.IsFailure)
                return validation;

            // TODO: implement handling logic (notification, projection update, etc.)

            return Result.Success();
        }
    }
}
