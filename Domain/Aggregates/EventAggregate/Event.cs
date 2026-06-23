using Domain.Common;
using Domain.Primitives;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.EventAggregate.Events;
using Domain.Aggregates.EventAggregate.Events.TicketTypeEvents;

namespace Domain.Aggregates.EventAggregate
{
    public class Event : AggregateRoot<EventId>
    {
        private const int MAX_TICKET_TYPES = 10;
        private const int MAX_NAME_LENGTH = 100;
        private const int MAX_DESCRIPTION_LENGTH = 500;
        private const int MIN_CAPACITY = 1;

        public EventName EventName { get; private set; } = null!;
        public int Capacity { get; private set; }
        public DateTime Date { get; private set; }
        public Address Location { get; private set; } = null!;
        public EventStatus Status { get; private set; }
        public string Description { get; private set; } = string.Empty;
        public DateTime? PublishedAt { get; private set; }
        public DateTime? CancelledAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public string? CancellationReason { get; private set; }


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
            int capacity) : base(id)
        {
            EventName = name;
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
            // Validate name
            if (string.IsNullOrWhiteSpace(name))
                return Result<Event>.Failure(EventErrors.NameCannotBeEmpty());

            if (name.Length > MAX_NAME_LENGTH)
                return Result<Event>.Failure(EventErrors.NameTooLong(MAX_NAME_LENGTH));

            // Validate capacity
            if (capacity < MIN_CAPACITY)
                return Result<Event>.Failure(EventErrors.InvalidTotalSeats(capacity));

            // Validate date
            if (date < DateTime.UtcNow)
                return Result<Event>.Failure(EventErrors.InvalidEventDate(date));

            // Validate location
            if (location == null || string.IsNullOrWhiteSpace(location.City))
                return Result<Event>.Failure(EventErrors.LocationCannotBeEmpty());

            // Validate description
            if (description.Length > MAX_DESCRIPTION_LENGTH)
                return Result<Event>.Failure(EventErrors.DescriptionTooLong(MAX_DESCRIPTION_LENGTH));

            // Create name value object
            var nameResult = EventName.Create(name);
            if (nameResult.IsFailure)
                return Result<Event>.Failure(nameResult.Errors.ToArray());

            // Create location value object
            var locationResult = Address.Create(
                location.Country,
                location.City,
                location.Street ?? string.Empty);
            if (locationResult.IsFailure)
                return Result<Event>.Failure(locationResult.Errors.ToArray());

            // Create event ID
            var eventIdResult = EventId.Create(Guid.NewGuid());
            if (eventIdResult.IsFailure)
                return Result<Event>.Failure(eventIdResult.Errors.ToArray());

            var newEvent = new Event(
                eventIdResult.Value,
                nameResult.Value,
                date,
                locationResult.Value,
                description,
                capacity);

            // Raise domain event
            newEvent.RaiseDomainEvent(new EventCreatedEvent(
                newEvent.Id,
                newEvent.EventName.Value,
                newEvent.Date,
                newEvent.Capacity));

            return Result<Event>.Success(newEvent);
        }

        public Result Publish()
        {
            // Validate current state
            if (Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.CannotPublishCancelledEvent());

            if (Status == EventStatus.Completed)
                return Result.Failure(EventErrors.CannotPublishCompletedEvent());

            if (Status == EventStatus.Published)
                return Result.Failure(EventErrors.AlreadyPublished());

            // Validate business rules
            if (_ticketTypes.Count == 0)
                return Result.Failure(EventErrors.CannotPublishWithoutTicketTypes());

            if (Date < DateTime.UtcNow)
                return Result.Failure(EventErrors.InvalidEventDate(Date));

            // Update state
            Status = EventStatus.Published;
            PublishedAt = DateTime.UtcNow;

            // Raise domain event
            RaiseDomainEvent(new EventPublishedEvent(Id, _ticketTypes.Count));

            return Result.Success();
        }

        public Result Cancel(string? reason = null)
        {
            // Validate current state
            if (Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.AlreadyCancelled());

            if (Status == EventStatus.Completed)
                return Result.Failure(EventErrors.CannotCancelCompletedEvent());

            if (Status == EventStatus.Published)
                return Result.Failure(EventErrors.CannotCancelPublishedEvent());

            // Update state
            Status = EventStatus.Cancelled;
            CancelledAt = DateTime.UtcNow;
            CancellationReason = reason;

            // Raise domain event
            RaiseDomainEvent(new EventCancelledEvent(Id, reason));

            return Result.Success();
        }

        public Result Complete()
        {
            // Validate current state
            if (Status == EventStatus.Completed)
                return Result.Failure(EventErrors.AlreadyCompleted());

            if (Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.CannotCompleteCancelledEvent());

            if (Status == EventStatus.Draft)
                return Result.Failure(EventErrors.CannotCompleteDraftEvent());

            if (Date > DateTime.UtcNow)
                return Result.Failure(EventErrors.CannotCompleteFutureEvent(Date));

            // Update state
            Status = EventStatus.Completed;
            CompletedAt = DateTime.UtcNow;

            // Raise domain event
            RaiseDomainEvent(new EventCompletedEvent(Id));

            return Result.Success();
        }

        public Result Reopen()
        {
            if (Status != EventStatus.Completed)
                return Result.Failure(EventErrors.CanOnlyReopenCompletedEvent());

            if (Date < DateTime.UtcNow)
                return Result.Failure(EventErrors.CannotReopenPastEvent(Date));

            Status = EventStatus.Published;
            CompletedAt = null;

            return Result.Success();
        }
        // ... existing code ...

        public Result AddTicketType(string name, Money price, int capacity)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyTicketTypesAfterDraft());

            var remainingCapacity = Capacity - _ticketTypes.Sum(t => t.Capacity);

            var ticketResult = TicketType.Create(Id, name, price, capacity, remainingCapacity);
            if (ticketResult.IsFailure)
                return Result.Failure(ticketResult.Errors.ToArray());

            _ticketTypes.Add(ticketResult.Value);

            RaiseDomainEvent(new TicketTypeAddedEvent(
                Id,
                ticketResult.Value.Id,
                name,
                price.Amount,
                capacity));

            return Result.Success();
        }

        public Result UpdateTicketTypePrice(TicketTypeId ticketTypeId, Money newPrice)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyTicketTypesAfterDraft());

            var ticketType = _ticketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType == null)
                return Result.Failure(TicketTypeErrors.TicketTypeNotFound(ticketTypeId));

            var oldPrice = ticketType.Price.Amount;

            var updateResult = ticketType.UpdatePrice(newPrice);
            if (updateResult.IsFailure)
                return updateResult;

            RaiseDomainEvent(new TicketTypePriceUpdatedEvent(
                ticketTypeId,
                Id,
                oldPrice,
                newPrice.Amount,
                newPrice.Currency));

            return Result.Success();
        }

        public Result UpdateTicketTypeCapacity(TicketTypeId ticketTypeId, int newCapacity)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyTicketTypesAfterDraft());

            var ticketType = _ticketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType == null)
                return Result.Failure(TicketTypeErrors.TicketTypeNotFound(ticketTypeId));

            var oldCapacity = ticketType.Capacity;
            var remainingCapacity = Capacity - _ticketTypes.Sum(t => t.Capacity) + oldCapacity;

            var updateResult = ticketType.UpdateCapacity(newCapacity, remainingCapacity);
            if (updateResult.IsFailure)
                return updateResult;

            RaiseDomainEvent(new TicketTypeCapacityUpdatedEvent(
                ticketTypeId,
                Id,
                oldCapacity,
                newCapacity));

            return Result.Success();
        }

        public Result RemoveTicketType(TicketTypeId ticketTypeId)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyTicketTypesAfterDraft());

            var ticketType = _ticketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType == null)
                return Result.Failure(TicketTypeErrors.TicketTypeNotFound(ticketTypeId));

            var removeResult = ticketType.Remove();
            if (removeResult.IsFailure)
                return removeResult;

            _ticketTypes.Remove(ticketType);

            RaiseDomainEvent(new TicketTypeRemovedEvent(
                ticketTypeId,    
                Id,              
                ticketType.TicketTypeName  
            ));

            return Result.Success();
        }

        public Result ReserveSeats(TicketTypeId ticketTypeId, int quantity)
        {
            var ticketType = _ticketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType == null)
                return Result.Failure(TicketTypeErrors.TicketTypeNotFound(ticketTypeId));

            var oldSoldCount = ticketType.SoldCount;

            var reserveResult = ticketType.ReserveSeats(quantity);
            if (reserveResult.IsFailure)
                return reserveResult;

            RaiseDomainEvent(new TicketTypeSeatsReservedEvent(
                ticketTypeId,
                Id,
                quantity,
                ticketType.SoldCount,
                ticketType.AvailableCount));

            return Result.Success();
        }

        public Result ReleaseSeats(TicketTypeId ticketTypeId, int quantity)
        {
            var ticketType = _ticketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType == null)
                return Result.Failure(TicketTypeErrors.TicketTypeNotFound(ticketTypeId));

            var releaseResult = ticketType.ReleaseSeats(quantity);
            if (releaseResult.IsFailure)
                return releaseResult;

            RaiseDomainEvent(new TicketTypeSeatsReleasedEvent(
                ticketTypeId,
                Id,
                quantity,
                ticketType.SoldCount,
                ticketType.AvailableCount));

            return Result.Success();
        }
        public Result UpdateCapacity(int newCapacityValue)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyCapacityAfterDraft());

            if (newCapacityValue < MIN_CAPACITY)
                return Result.Failure(EventErrors.InvalidTotalSeats(newCapacityValue));

            var allocatedTickets = _ticketTypes.Sum(t => t.Capacity);
            if (newCapacityValue < allocatedTickets)
                return Result.Failure(EventErrors.TotalSeatsCannotBeLessThanAllocatedTickets(
                    newCapacityValue, allocatedTickets));

            var oldCapacity = Capacity;
            Capacity = newCapacityValue;

            // Raise domain event
            RaiseDomainEvent(new EventCapacityUpdatedEvent(Id, oldCapacity, newCapacityValue));

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

            if (newDescription.Length > MAX_DESCRIPTION_LENGTH)
                return Result.Failure(EventErrors.DescriptionTooLong(MAX_DESCRIPTION_LENGTH));

            Description = newDescription;
            return Result.Success();
        }

        public Result UpdateName(string newName)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyNameAfterDraft());

            if (string.IsNullOrWhiteSpace(newName))
                return Result.Failure(EventErrors.NameCannotBeEmpty());

            if (newName.Length > MAX_NAME_LENGTH)
                return Result.Failure(EventErrors.NameTooLong(MAX_NAME_LENGTH));

            var nameResult = EventName.Create(newName);
            if (nameResult.IsFailure)
                return Result.Failure(nameResult.Errors.ToArray());

            EventName = nameResult.Value;
            return Result.Success();
        }

        public Result UpdateLocation(Address newLocation)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyLocationAfterDraft());

            if (newLocation == null || string.IsNullOrWhiteSpace(newLocation.City))
                return Result.Failure(EventErrors.LocationCannotBeEmpty());

            var locationResult = Address.Create(
                newLocation.Country,
                newLocation.City,
                newLocation.Street ?? string.Empty);
            if (locationResult.IsFailure)
                return Result.Failure(locationResult.Errors.ToArray());

            Location = locationResult.Value;
            return Result.Success();
        }

        public bool CanBeModified()
        {
            return Status == EventStatus.Draft;
        }

        public bool IsActive()
        {
            return Status == EventStatus.Published || Status == EventStatus.Draft;
        }

        public bool HasAvailableSeats()
        {
            var totalSold = _ticketTypes.Sum(t => t.Capacity);
            return totalSold < Capacity;
        }

        public int GetRemainingCapacity()
        {
            return Capacity - _ticketTypes.Sum(t => t.Capacity);
        }
    }
}