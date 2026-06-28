using Domain.Common;
using Domain.Primitives;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Errors;

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
        public DateTime CreatedAt { get; private set; }
        public DateTime? LastModifiedAt { get; private set; }

        public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

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
            int capacity,
            DateTime createdAt) : base(id)
        {
            EventName = name;
            Date = date;
            Location = location;
            Description = description;
            Capacity = capacity;
            Status = EventStatus.Draft;
            CreatedAt = createdAt;
            RowVersion = Array.Empty<byte>();
        }

        public static Result<Event> Create(
            string name,
            int capacity,
            DateTime date,
            Address location,
            string description,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            if (capacity < MIN_CAPACITY)
                return Result<Event>.Failure(EventErrors.InvalidTotalSeats(capacity));

            if (date < dateTimeProvider.UtcNow)
                return Result<Event>.Failure(EventErrors.InvalidEventDate(date));

            if (location == null || string.IsNullOrWhiteSpace(location.City))
                return Result<Event>.Failure(EventErrors.LocationCannotBeEmpty());

            if (description.Length > MAX_DESCRIPTION_LENGTH)
                return Result<Event>.Failure(EventErrors.DescriptionTooLong(MAX_DESCRIPTION_LENGTH));

            var nameResult = EventName.Create(name);
            if (nameResult.IsFailure)
                return Result<Event>.Failure(nameResult.Errors.ToArray());

            var locationResult = Address.Create(
                location.Country,
                location.City,
                location.Street ?? string.Empty);
            if (locationResult.IsFailure)
                return Result<Event>.Failure(locationResult.Errors.ToArray());

            var eventIdResult = EventId.Create(Guid.NewGuid());
            if (eventIdResult.IsFailure)
                return Result<Event>.Failure(eventIdResult.Errors.ToArray());

            var newEvent = new Event(
                eventIdResult.Value,
                nameResult.Value,
                date,
                locationResult.Value,
                description,
                capacity,
                dateTimeProvider.UtcNow); 

            newEvent.RaiseDomainEvent(DomainEventFactory.CreateEventCreated(
                newEvent.Id,
                newEvent.EventName.Value,
                newEvent.Date,
                newEvent.Capacity,
                dateTimeProvider.UtcNow,
                metadata));

            return Result<Event>.Success(newEvent);
        }

        public Result Publish(IDateTimeProvider dateTimeProvider, EventMetadata metadata)
        {
            if (Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.CannotPublishCancelledEvent());

            if (Status == EventStatus.Completed)
                return Result.Failure(EventErrors.CannotPublishCompletedEvent());

            if (Status == EventStatus.Published)
                return Result.Failure(EventErrors.AlreadyPublished());

            if (_ticketTypes.Count == 0)
                return Result.Failure(EventErrors.CannotPublishWithoutTicketTypes());

            if (Date < dateTimeProvider.UtcNow)
                return Result.Failure(EventErrors.InvalidEventDate(Date));

            Status = EventStatus.Published;
            PublishedAt = dateTimeProvider.UtcNow;
            LastModifiedAt = dateTimeProvider.UtcNow; 

            RaiseDomainEvent(DomainEventFactory.CreateEventPublished(
                Id,
                _ticketTypes.Count,
                dateTimeProvider.UtcNow,
                metadata));

            return Result.Success();
        }

        public Result Cancel(IDateTimeProvider dateTimeProvider, EventMetadata metadata, string? reason = null)
        {
            if (Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.AlreadyCancelled());

            if (Status == EventStatus.Completed)
                return Result.Failure(EventErrors.CannotCancelCompletedEvent());

            if (Status == EventStatus.Published)
                return Result.Failure(EventErrors.CannotCancelPublishedEvent());

            Status = EventStatus.Cancelled;
            CancelledAt = dateTimeProvider.UtcNow;
            CancellationReason = reason;
            LastModifiedAt = dateTimeProvider.UtcNow; 

            RaiseDomainEvent(DomainEventFactory.CreateEventCancelled(
                Id,
                reason,
                dateTimeProvider.UtcNow,
                metadata));

            return Result.Success();
        }

        public Result Complete(IDateTimeProvider dateTimeProvider, EventMetadata metadata)
        {
            if (Status == EventStatus.Completed)
                return Result.Failure(EventErrors.AlreadyCompleted());

            if (Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.CannotCompleteCancelledEvent());

            if (Status == EventStatus.Draft)
                return Result.Failure(EventErrors.CannotCompleteDraftEvent());

            if (Date > dateTimeProvider.UtcNow)
                return Result.Failure(EventErrors.CannotCompleteFutureEvent(Date));

            Status = EventStatus.Completed;
            CompletedAt = dateTimeProvider.UtcNow;
            LastModifiedAt = dateTimeProvider.UtcNow;

            RaiseDomainEvent(DomainEventFactory.CreateEventCompleted(
                Id,
                dateTimeProvider.UtcNow,
                metadata));

            return Result.Success();
        }

        public Result Reopen(IDateTimeProvider dateTimeProvider)
        {
            if (Status != EventStatus.Completed)
                return Result.Failure(EventErrors.CanOnlyReopenCompletedEvent());

            if (Date < dateTimeProvider.UtcNow) 
                return Result.Failure(EventErrors.CannotReopenPastEvent(Date));

            Status = EventStatus.Published;
            CompletedAt = null;
            LastModifiedAt = dateTimeProvider.UtcNow;

            return Result.Success();
        }


        public Result AddTicketType(
            string name,
            Money price,
            int capacity,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyTicketTypesAfterDraft());

            if (_ticketTypes.Count >= MAX_TICKET_TYPES)
                return Result.Failure(EventErrors.MaxTicketTypesReached(MAX_TICKET_TYPES));

            var remainingCapacity = Capacity - _ticketTypes.Sum(t => t.Capacity);

            var ticketResult = TicketType.Create(
                Id,
                name,
                price,
                capacity,
                dateTimeProvider,
                remainingCapacity);

            if (ticketResult.IsFailure)
                return Result.Failure(ticketResult.Errors.ToArray());

            _ticketTypes.Add(ticketResult.Value);
            LastModifiedAt = dateTimeProvider.UtcNow; 

            RaiseDomainEvent(DomainEventFactory.CreateTicketTypeAdded(
                Id,
                ticketResult.Value.Id,
                name,
                price.Amount,
                capacity,
                dateTimeProvider.UtcNow,
                metadata));

            return Result.Success();
        }

        public Result UpdateTicketTypePrice(
            TicketTypeId ticketTypeId,
            Money newPrice,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyTicketTypesAfterDraft());

            var ticketType = _ticketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType == null)
                return Result.Failure(TicketTypeErrors.TicketTypeNotFound(ticketTypeId));

            var oldPrice = ticketType.Price.Amount;

            var updateResult = ticketType.UpdatePrice(newPrice, dateTimeProvider);
            if (updateResult.IsFailure)
                return updateResult;

            LastModifiedAt = dateTimeProvider.UtcNow; 

            RaiseDomainEvent(DomainEventFactory.CreateTicketTypePriceUpdated(
                ticketTypeId,
                Id,
                oldPrice,
                newPrice.Amount,
                newPrice.Currency,
                dateTimeProvider.UtcNow,
                metadata));

            return Result.Success();
        }

        public Result UpdateTicketTypeCapacity(
            TicketTypeId ticketTypeId,
            int newCapacity,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyTicketTypesAfterDraft());

            var ticketType = _ticketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType == null)
                return Result.Failure(TicketTypeErrors.TicketTypeNotFound(ticketTypeId));

            var oldCapacity = ticketType.Capacity;
            var remainingCapacity = Capacity - _ticketTypes.Sum(t => t.Capacity) + oldCapacity;
            var updateResult = ticketType.UpdateCapacity(newCapacity, dateTimeProvider, remainingCapacity);
            if (updateResult.IsFailure)
                return updateResult;

            LastModifiedAt = dateTimeProvider.UtcNow; 

            RaiseDomainEvent(DomainEventFactory.CreateTicketTypeCapacityUpdated(
                ticketTypeId,
                Id,
                oldCapacity,
                newCapacity,
                dateTimeProvider.UtcNow,
                metadata));

            return Result.Success();
        }

        public Result RemoveTicketType(
            TicketTypeId ticketTypeId,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
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
            LastModifiedAt = dateTimeProvider.UtcNow; 

            RaiseDomainEvent(DomainEventFactory.CreateTicketTypeRemoved(
                ticketTypeId,
                Id,
                ticketType.TicketTypeName,
                dateTimeProvider.UtcNow,
                metadata));

            return Result.Success();
        }


        public Result ReserveSeats(
            TicketTypeId ticketTypeId,
            int quantity,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            var ticketType = _ticketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType == null)
                return Result.Failure(TicketTypeErrors.TicketTypeNotFound(ticketTypeId));

            var reserveResult = ticketType.ReserveSeats(quantity, dateTimeProvider);
            if (reserveResult.IsFailure)
                return reserveResult;

            RaiseDomainEvent(DomainEventFactory.CreateTicketTypeSeatsReserved(
                ticketTypeId,
                Id,
                quantity,
                ticketType.SoldCount,
                ticketType.AvailableCount,
                dateTimeProvider.UtcNow,
                metadata));

            return Result.Success();
        }

        public Result ReleaseSeats(
            TicketTypeId ticketTypeId,
            int quantity,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
        {
            var ticketType = _ticketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType == null)
                return Result.Failure(TicketTypeErrors.TicketTypeNotFound(ticketTypeId));

            var releaseResult = ticketType.ReleaseSeats(quantity, dateTimeProvider);
            if (releaseResult.IsFailure)
                return releaseResult;

            RaiseDomainEvent(DomainEventFactory.CreateTicketTypeSeatsReleased(
                ticketTypeId,
                Id,
                quantity,
                ticketType.SoldCount,
                ticketType.AvailableCount,
                dateTimeProvider.UtcNow,
                metadata));

            return Result.Success();
        }


        public Result UpdateCapacity(
            int newCapacityValue,
            IDateTimeProvider dateTimeProvider,
            EventMetadata metadata)
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
            LastModifiedAt = dateTimeProvider.UtcNow; 

            RaiseDomainEvent(DomainEventFactory.CreateEventCapacityUpdated(
                Id,
                oldCapacity,
                newCapacityValue,
                dateTimeProvider.UtcNow,
                metadata));

            return Result.Success();
        }

        public Result UpdateDate(DateTime newDate, IDateTimeProvider dateTimeProvider)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyDateAfterDraft());

            if (newDate < dateTimeProvider.UtcNow)
                return Result.Failure(EventErrors.InvalidEventDate(newDate));

            Date = newDate;
            LastModifiedAt = dateTimeProvider.UtcNow;

            return Result.Success();
        }

        public Result UpdateDescription(string newDescription, IDateTimeProvider dateTimeProvider)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyDescriptionAfterDraft());

            if (newDescription.Length > MAX_DESCRIPTION_LENGTH)
                return Result.Failure(EventErrors.DescriptionTooLong(MAX_DESCRIPTION_LENGTH));

            Description = newDescription;
            LastModifiedAt = dateTimeProvider.UtcNow; 

            return Result.Success();
        }

        public Result UpdateName(string newName, IDateTimeProvider dateTimeProvider)
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
            LastModifiedAt = dateTimeProvider.UtcNow; 

            return Result.Success();
        }

        public Result UpdateLocation(Address newLocation, IDateTimeProvider dateTimeProvider)
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
            LastModifiedAt = dateTimeProvider.UtcNow; 

            return Result.Success();
        }


        public bool HasAvailableSeats()
        {
            return _ticketTypes.Any(t => t.SoldCount < t.Capacity);
        }

        public int GetRemainingCapacity()
        {
            return _ticketTypes.Sum(t => t.Capacity - t.SoldCount);
        }

        public int GetReservedCount()
        {
            return _ticketTypes.Sum(t => t.ReservedCount);
        }

        public int GetAvailableSeats()
        {
            return _ticketTypes.Sum(t => t.Capacity - t.SoldCount - t.ReservedCount);
        }

        public bool HasAvailableSeatsReal()
        {
            return GetAvailableSeats() > 0;
        }

        public int GetTotalSoldCount()
        {
            return _ticketTypes.Sum(t => t.SoldCount);
        }

        public int GetTotalTicketCapacity()
        {
            return _ticketTypes.Sum(t => t.Capacity);
        }

        public bool IsFullyBooked()
        {
            return GetAvailableSeats() <= 0;
        }

        public double GetOccupancyRate()
        {
            var totalCapacity = GetTotalTicketCapacity();
            if (totalCapacity == 0) return 0;

            return (double)GetTotalSoldCount() / totalCapacity * 100;
        }
    }
}