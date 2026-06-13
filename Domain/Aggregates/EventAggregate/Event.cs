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
        public EventCapacity Capacity { get; private set; } = null!;
        public DateTime Date { get; private set; }
        public Address Location { get; private set; } = null!;
        public EventStatus Status { get; private set; }
        public string Description { get; private set; } = string.Empty;
        public int TotalSeats => Capacity.Capacity;
        private readonly List<TicketType> _ticketTypes = new();
        public IReadOnlyCollection<TicketType> TicketTypes => _ticketTypes.AsReadOnly();

        protected Event() : base(default!) { }

        private Event(EventId id) : base(id) { }
            
        private Event(
            EventId id,
            EventName name,
            DateTime date,
            Address location,
            string description,
            EventCapacity capacity) : base(id)
        {
            Name = name;
            Date = date;
            Location= location;
            Description = description;
            Capacity = capacity;
            Status = EventStatus.Draft;
        }

        public static Result<Event> Create(
            string name,
            DateTime date,
            Address location,
            int capacityValue,
            string description = "")
        {
            var nameResult = EventName.Create(name);
            if (nameResult.IsFailure)
                return Result<Event>.Failure(nameResult.Error);

            var capacityResult = EventCapacity.Create(capacityValue);
            if (capacityResult.IsFailure)
                return Result<Event>.Failure(capacityResult.Error);
            
            if (date < DateTime.UtcNow)
                return Result<Event>.Failure("Event date must be in the future.");
            
            var locationResult = Address.Create(location.Country, location.City, location.Street);
            if (locationResult.IsFailure)
                return Result<Event>.Failure(locationResult.Error);
                
                var newEvent = new Event(
                EventId.CreateUnqiue(),
                nameResult.Value,
                date,
                locationResult.Value,
                description,
                capacityResult.Value);

            return Result<Event>.Success(newEvent);
        }

        public Result Publish()
        {
            if (Date < DateTime.UtcNow)
                return Result.Failure(EventErrors.InvalidEventDate(Date));

            if (Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.CannotPublishCancelledEvent());

            if (_ticketTypes.Count == 0)
                return Result.Failure(EventErrors.CannotPublishWithoutTicketTypes());

            Status = EventStatus.Published;
            return Result.Success();
        }

        public Result Cancel()
        {
            if (Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.AlreadyCancelled());

            if (Status == EventStatus.Published)
                return Result.Failure(EventErrors.CannotCancelPublishedEvent());

            Status = EventStatus.Cancelled;
            return Result.Success();
        }

        public Result AddTicketType(string name, Money price, int capacity)
        {
            if (_ticketTypes.Count >= 10)
                return Result.Failure(EventErrors.MaxTicketTypesExceeded(10));

            var remainingCapacity = TotalSeats - _ticketTypes.Sum(t => t.Capacity);
            if (capacity > remainingCapacity)
                return Result.Failure(EventErrors.TicketTypeCapacityExceedsRemainingCapacity(
                    capacity, remainingCapacity));

            var ticketTypeResult = TicketType.Create(Id,name, price, capacity);
            if (ticketTypeResult.IsFailure)
                return Result.Failure(ticketTypeResult.Error);
            return Result.Success();
        }

        public Result UpdateTitle(string newTitle)
        {
            var nameResult = EventName.Create(newTitle);
            if (nameResult.IsFailure)
                return Result.Failure(nameResult.Error);

            Name = nameResult.Value;
            return Result.Success();
        }

        public Result UpdateCapacity(int newCapacityValue)
        {
            if (Status==EventStatus.Published)
                return Result.Failure(EventErrors.CannotChangeCapacityAfterPublish());

            var capacityResult = EventCapacity.Create(newCapacityValue);
            if (capacityResult.IsFailure)
                return Result.Failure(capacityResult.Error);

            var allocatedTickets = _ticketTypes.Sum(t => t.Capacity);
            if (newCapacityValue < allocatedTickets)
                return Result.Failure(EventErrors.TotalSeatsCannotBeLessThanAllocatedTickets(
                    newCapacityValue, allocatedTickets));

            Capacity = capacityResult.Value;
            return Result.Success();
        }

        public Result UpdateLocation(Address newLocation)
        {
            var locationResult = Address.Create(newLocation.Country, newLocation.City, newLocation.Street);
            if(locationResult.IsFailure)
                return Result.Failure(locationResult.Error);
            
            Location = locationResult.Value;
            return Result.Success();
        }

        public Result UpdateDate(DateTime newDate)
        {
            if (newDate < DateTime.UtcNow)
                return Result.Failure(EventErrors.InvalidEventDate(Date));

            Date = newDate;
            return Result.Success();
        }

        public Result UpdateStatus(EventStatus newStatus)
        {
            if (newStatus == EventStatus.Published && Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.CannotPublishCancelledEvent());
            if (newStatus == EventStatus.Cancelled && Status == EventStatus.Published)
                return Result.Failure(EventErrors.CannotCancelPublishedEvent());
            if (newStatus == EventStatus.Published && _ticketTypes.Count == 0)
                return Result.Failure(EventErrors.CannotPublishWithoutTicketTypes());
            if (newStatus == EventStatus.Published && Date < DateTime.UtcNow)
                return Result.Failure(EventErrors.InvalidEventDate(Date));
            if (newStatus == EventStatus.Cancelled && Status == EventStatus.Completed)
                return Result.Failure(EventErrors.CannotCancelCompletedEvent());
            if (newStatus == EventStatus.Completed && Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.CannotCompleteCancelledEvent());
            if (newStatus == EventStatus.Completed && Status == EventStatus.Draft)
                return Result.Failure(EventErrors.CannotCompleteDraftEvent());
            Status = newStatus;
            return Result.Success();
        }


        }
}
