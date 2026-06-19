using Domain.Common;
using Domain.Primitives;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.Errors;
using Domain.Aggregates.EventAggregate.Enums;

namespace Domain.Aggregates.EventAggregate
{
    public class Event : AggregateRoot<EventId>
    {
        public EventName Name { get; private set; } = null!;
        public int Capacity { get; private set; }
        public DateTime Date { get; private set; }
        public Address Location { get; private set; } = null!;
        public EventStatus Status { get; private set; }
        public string Description { get; private set; } = string.Empty;

        private readonly List<TicketType> _ticketTypes = new List<TicketType>();
        public IReadOnlyCollection<TicketType> TicketTypes => _ticketTypes.AsReadOnly();

        protected Event() : base(default!) { }

        private Event(EventId id) : base(id) { }

        private Event(
            EventId id,
            EventName name,
            DateTime date,
            Address location,
            string description,
            int capacity) : base(id)
        {
            Name = name;
            Date = date;
            Location = location;
            Description = description;
            Capacity = capacity;
            Status = EventStatus.Draft;
        }

        public static Result<Event> Create(
            string name,
            int capacity,
            DateTime date,
            Address location,
            string description = "")
        {
            var nameResult = EventName.Create(name);
            if (nameResult.IsFailure)
                return Result<Event>.Failure(nameResult.Error);

            if (capacity < 0)
                return Result<Event>.Failure(EventErrors.TotalSeatsCannotBeNegative(capacity));
            
            if (date < DateTime.UtcNow)
                return Result<Event>.Failure(EventErrors.InvalidEventDate(date));

            var locationResult = Address.Create(location.Country, location.City, location.Street);
            if (locationResult.IsFailure)
                return Result<Event>.Failure(locationResult.Error);

            var EventIdResult = EventId.Create(Guid.NewGuid());
            if (EventIdResult.IsFailure)
                return Result<Event>.Failure(EventIdResult.Error);

            var newEvent = new Event(
            EventIdResult.Value,
            nameResult.Value,
            date,
            locationResult.Value,
            description,
            capacity);

            return Result<Event>.Success(newEvent);
        }

        public Result Publish()
        {
            if (Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.CannotPublishCancelledEvent());

            if (Status == EventStatus.Completed)
                return Result.Failure(EventErrors.CannotPublishCompletedEvent());

            if (Status == EventStatus.Published)
                return Result.Failure(EventErrors.AlreadyPublished());

            if (_ticketTypes.Count == 0)
                return Result.Failure(EventErrors.CannotPublishWithoutTicketTypes());

            if (Date < DateTime.UtcNow)
                return Result.Failure(EventErrors.InvalidEventDate(Date));

            Status = EventStatus.Published;


            return Result.Success();
        }

        public Result Cancel()
        {
            if (Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.AlreadyCancelled());

            if (Status == EventStatus.Completed)
                return Result.Failure(EventErrors.CannotCancelCompletedEvent());

            Status = EventStatus.Cancelled;

            return Result.Success();
        }

        public Result Complete()
        {
            if (Status == EventStatus.Completed)
                return Result.Failure(EventErrors.AlreadyCompleted());

            if (Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.CannotCompleteCancelledEvent());

            if (Status == EventStatus.Draft)
                return Result.Failure(EventErrors.CannotCompleteDraftEvent());

            if (Date > DateTime.UtcNow)
                return Result.Failure(EventErrors.CannotCompleteFutureEvent(Date));

            Status = EventStatus.Completed;


            return Result.Success();
        }

        public Result AddTicketType(string name, Money price, int capacity)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyTicketTypesAfterDraft());

            if (_ticketTypes.Count >= 10)
                return Result.Failure(EventErrors.MaxTicketTypesExceeded(10));

            var remainingCapacity = Capacity - _ticketTypes.Sum(t => t.Capacity);
            if (capacity > remainingCapacity)
                return Result.Failure(EventErrors.TicketTypeCapacityExceedsRemainingCapacity(
                    capacity, remainingCapacity));

            var TicketResult = TicketType.Create(this.Id, name, price, capacity);
            if (TicketResult.IsFailure)
                return Result.Failure(TicketResult.Error);

            _ticketTypes.Add(TicketResult.Value);
            return Result.Success();
        }

        public Result UpdateCapacity(int newCapacityValue)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyCapacityAfterDraft());

            if (newCapacityValue < 0)
                return Result<Event>.Failure(EventErrors.TotalSeatsCannotBeNegative(newCapacityValue));


            var allocatedTickets = _ticketTypes.Sum(t => t.Capacity);
            if (newCapacityValue < allocatedTickets)
                return Result.Failure(EventErrors.TotalSeatsCannotBeLessThanAllocatedTickets(
                    newCapacityValue, allocatedTickets));

            Capacity = newCapacityValue;
            return Result.Success();
        }

        public Result UpdateDate(DateTime newDate)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyDateAfterDraft());

            if (newDate < DateTime.UtcNow)
                return Result.Failure(EventErrors.InvalidEventDate(newDate));

            Date = newDate;
            return Result.Success();
        }


        public Result UpdateDescription(string newDescription)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyDescriptionAfterDraft());

            if (newDescription.Length > 500)
                return Result.Failure(EventErrors.DescriptionTooLong(500));

            Description = newDescription;
            return Result.Success();
        }

        public Result UpdateTitle(string newTitle)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyCapacityAfterDraft());

            var nameResult = EventName.Create(newTitle);
            if (nameResult.IsFailure)
                return Result.Failure(nameResult.Error);

            Name = nameResult.Value;
            return Result.Success();
        }

        public Result UpdateLocation(Address newLocation)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyCapacityAfterDraft());

            var locationResult = Address.Create(newLocation.Country, newLocation.City, newLocation.Street);
            if (locationResult.IsFailure)
                return Result.Failure(locationResult.Error);

            Location = locationResult.Value;
            return Result.Success();
        }

    }
}
