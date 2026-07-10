using Domain.Common;
using Domain.Primitives;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.EventAggregate.Entities;
using Domain.Aggregates.EventAggregate.Events;
using Domain.Aggregates.EventAggregate.Events.TicketTypeEvents;
using Domain.Errors;

namespace Domain.Aggregates.EventAggregate
{
    public class Event : AggregateRoot<EventId>
    {
        private const int MAX_TICKET_TYPES = 10;
        private const int MAX_PHOTOS = 10;
        private const int MAX_NAME_LENGTH = 100;
        private const int MAX_DESCRIPTION_LENGTH = 500;
        private const int MIN_CAPACITY = 1;

        public EventName EventName { get; private set; } = null!;
        public int Capacity { get; private set; }
        public DateTime Date { get; private set; }
        public Address Location { get; private set; } = null!;
        public EventStatus Status { get; private set; }
        public EventType Type { get; private set; }
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

        private readonly List<EventPhoto> _photos = new();
        public IReadOnlyList<EventPhoto> Photos => _photos.AsReadOnly();

        protected Event() : base(default!) { }

        private Event(EventId id) : base(id) { }

        private Event(
            EventId id,
            EventName name,
            DateTime date,
            Address location,
            string description,
            int capacity,
            EventType eventType,
            DateTime createdAt) : base(id)
        {
            EventName = name;
            Date = date;
            Location = location;
            Description = description;
            Capacity = capacity;
            Type = eventType;
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
            EventType eventType,
            DateTime dateTime)
        {
            if (capacity < MIN_CAPACITY)
                return Result<Event>.Failure(EventErrors.InvalidTotalSeats(capacity));

            if (date < dateTime)
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
                location.Street ?? string.Empty,
                latitude: location.Latitude,
                longitude: location.Longitude);
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
                eventType,
                dateTime); 

            newEvent.RaiseDomainEvent(new EventCreatedEvent(
                newEvent.Id,
                newEvent.EventName.Value,
                newEvent.Date,
                newEvent.Capacity,
                dateTime));

            return Result<Event>.Success(newEvent);
        }

        public Result Publish(DateTime dateTime)
        {
            if (Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.CannotPublishCancelledEvent());

            if (Status == EventStatus.Completed)
                return Result.Failure(EventErrors.CannotPublishCompletedEvent());

            if (Status == EventStatus.Published)
                return Result.Failure(EventErrors.AlreadyPublished());

            if (_ticketTypes.Count == 0)
                return Result.Failure(EventErrors.CannotPublishWithoutTicketTypes());

            if (Date < dateTime)
                return Result.Failure(EventErrors.InvalidEventDate(Date));

            Status = EventStatus.Published;
            PublishedAt = dateTime;
            LastModifiedAt = dateTime; 

            RaiseDomainEvent(new EventPublishedEvent(
                Id,
                _ticketTypes.Count,
                dateTime));

            return Result.Success();
        }

        public Result Cancel(DateTime dateTime, string? reason = null)
        {
            if (Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.AlreadyCancelled());

            if (Status == EventStatus.Completed)
                return Result.Failure(EventErrors.CannotCancelCompletedEvent());

            if (Status == EventStatus.Published)
                return Result.Failure(EventErrors.CannotCancelPublishedEvent());

            Status = EventStatus.Cancelled;
            CancelledAt = dateTime;
            CancellationReason = reason;
            LastModifiedAt = dateTime; 

            RaiseDomainEvent(new EventCancelledEvent(
                Id,
                dateTime,
                reason));

            return Result.Success();
        }

        public Result AdminCancel(DateTime dateTime, string? reason = null)
        {
            if (Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.AlreadyCancelled());

            if (Status == EventStatus.Completed)
                return Result.Failure(EventErrors.CannotCancelCompletedEvent());

            Status = EventStatus.Cancelled;
            CancelledAt = dateTime;
            CancellationReason = reason;
            LastModifiedAt = dateTime;

            RaiseDomainEvent(new EventCancelledEvent(
                Id,
                dateTime,
                reason));

            return Result.Success();
        }

        public Result Complete(DateTime dateTime)
        {
            if (Status == EventStatus.Completed)
                return Result.Failure(EventErrors.AlreadyCompleted());

            if (Status == EventStatus.Cancelled)
                return Result.Failure(EventErrors.CannotCompleteCancelledEvent());

            if (Status == EventStatus.Draft)
                return Result.Failure(EventErrors.CannotCompleteDraftEvent());

            if (Date > dateTime)
                return Result.Failure(EventErrors.CannotCompleteFutureEvent(Date));

            Status = EventStatus.Completed;
            CompletedAt = dateTime;
            LastModifiedAt = dateTime;

            RaiseDomainEvent(new EventCompletedEvent(
                Id,
                dateTime));

            return Result.Success();
        }

        public Result Reopen(DateTime dateTime)
        {
            if (Status != EventStatus.Completed)
                return Result.Failure(EventErrors.CanOnlyReopenCompletedEvent());

            if (Date < dateTime) 
                return Result.Failure(EventErrors.CannotReopenPastEvent(Date));

            Status = EventStatus.Published;
            CompletedAt = null;
            LastModifiedAt = dateTime;

            return Result.Success();
        }


        public Result AddTicketType(
            string name,
            Money price,
            int capacity,
            DateTime dateTime)
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
                dateTime,
                remainingCapacity);

            if (ticketResult.IsFailure)
                return Result.Failure(ticketResult.Errors.ToArray());

            _ticketTypes.Add(ticketResult.Value);
            LastModifiedAt = dateTime; 

            RaiseDomainEvent(new TicketTypeAddedEvent(
                Id,
                ticketResult.Value.Id,
                name,
                price.Amount,
                capacity,
                dateTime));

            return Result.Success();
        }

        public Result UpdateTicketTypePrice(
            TicketTypeId ticketTypeId,
            Money newPrice,
            DateTime dateTime)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyTicketTypesAfterDraft());

            var ticketType = _ticketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType == null)
                return Result.Failure(TicketTypeErrors.TicketTypeNotFound(ticketTypeId));

            var oldPrice = ticketType.Price.Amount;

            var updateResult = ticketType.UpdatePrice(newPrice, dateTime);
            if (updateResult.IsFailure)
                return updateResult;

            LastModifiedAt = dateTime; 

            RaiseDomainEvent(new TicketTypePriceUpdatedEvent(
                ticketTypeId,
                Id,
                oldPrice,
                newPrice.Amount,
                newPrice.Currency,
                dateTime));

            return Result.Success();
        }

        public Result UpdateTicketTypeCapacity(
            TicketTypeId ticketTypeId,
            int newCapacity,
            DateTime utcNow)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyTicketTypesAfterDraft());

            var ticketType = _ticketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType == null)
                return Result.Failure(TicketTypeErrors.TicketTypeNotFound(ticketTypeId));

            var oldCapacity = ticketType.Capacity;
            var remainingCapacity = Capacity - _ticketTypes.Sum(t => t.Capacity) + oldCapacity;
            var updateResult = ticketType.UpdateCapacity(newCapacity, utcNow, remainingCapacity);
            if (updateResult.IsFailure)
                return updateResult;

            LastModifiedAt = utcNow; 

            RaiseDomainEvent(new TicketTypeCapacityUpdatedEvent(
                ticketTypeId,
                Id,
                oldCapacity,
                newCapacity,
                utcNow));

            return Result.Success();
        }

        public Result RemoveTicketType(
            TicketTypeId ticketTypeId,
            DateTime utcNow)
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
            LastModifiedAt = utcNow; 

            RaiseDomainEvent(new TicketTypeRemovedEvent(
                ticketTypeId,
                Id,
                ticketType.TicketTypeName,
                utcNow));

            return Result.Success();
        }


        public Result ReserveSeats(
            TicketTypeId ticketTypeId,
            int quantity,
            DateTime utcNow)
        {
            var ticketType = _ticketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType is null)
                return Result.Failure(TicketTypeErrors.TicketTypeNotFound(ticketTypeId));

            var reserveResult = ticketType.ReserveSeats(quantity, utcNow);
            if (reserveResult.IsFailure)
                return reserveResult;

            RaiseDomainEvent(new TicketTypeSeatsReservedEvent(
                ticketTypeId,
                Id,
                quantity,
                ticketType.SoldCount,
                ticketType.AvailableCount,
                utcNow));

            return Result.Success();
        }

        public Result ReleaseSeats(
            TicketTypeId ticketTypeId,
            int quantity,
            DateTime utcNow)
        {
            var ticketType = _ticketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType is null)
                return Result.Failure(TicketTypeErrors.TicketTypeNotFound(ticketTypeId));

            var releaseResult = ticketType.ReleaseSeats(quantity, utcNow);
            if (releaseResult.IsFailure)
                return releaseResult;

            RaiseDomainEvent(new TicketTypeSeatsReleasedEvent(
                ticketTypeId,
                Id,
                quantity,
                ticketType.SoldCount,
                ticketType.AvailableCount,
                utcNow));

            return Result.Success();
        }

        public Result ConfirmReservation(
            TicketTypeId ticketTypeId,
            int quantity,
            DateTime utcNow)
        {
            var ticketType = _ticketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType is null)
                return Result.Failure(TicketTypeErrors.TicketTypeNotFound(ticketTypeId));

            var confirmResult = ticketType.ConfirmReservation(quantity, utcNow);
            if (confirmResult.IsFailure)
                return confirmResult;

            RaiseDomainEvent(new TicketTypeSeatsReservedEvent(
                ticketTypeId,
                Id,
                quantity,
                ticketType.SoldCount,
                ticketType.AvailableCount,
                utcNow));

            return Result.Success();
        }

        public Result RefundSeats(
            TicketTypeId ticketTypeId,
            int quantity,
            DateTime utcNow)
        {
            var ticketType = _ticketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType is null)
                return Result.Failure(TicketTypeErrors.TicketTypeNotFound(ticketTypeId));

            var refundResult = ticketType.RefundSeats(quantity, utcNow);
            if (refundResult.IsFailure)
                return refundResult;

            RaiseDomainEvent(new TicketTypeSeatsReleasedEvent(
                ticketTypeId,
                Id,
                quantity,
                ticketType.SoldCount,
                ticketType.AvailableCount,
                utcNow));

            return Result.Success();
        }


        public Result UpdateCapacity(
            int newCapacityValue,
            DateTime utcNow)
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
            LastModifiedAt = utcNow; 

            RaiseDomainEvent(new EventCapacityUpdatedEvent(
                Id,
                oldCapacity,
                newCapacityValue,
                utcNow));

            return Result.Success();
        }

        public Result UpdateDate(DateTime newDate, DateTime utcNow)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyDateAfterDraft());

            if (newDate < utcNow)
                return Result.Failure(EventErrors.InvalidEventDate(newDate));

            Date = newDate;
            LastModifiedAt = utcNow;

            return Result.Success();
        }

        public Result UpdateDescription(string newDescription, DateTime utcNow)
        {
            if (Status != EventStatus.Draft)
                return Result.Failure(EventErrors.CannotModifyDescriptionAfterDraft());

            if (newDescription.Length > MAX_DESCRIPTION_LENGTH)
                return Result.Failure(EventErrors.DescriptionTooLong(MAX_DESCRIPTION_LENGTH));

            Description = newDescription;
            LastModifiedAt = utcNow; 

            return Result.Success();
        }

        public Result UpdateName(string newName, DateTime utcNow)
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
            LastModifiedAt = utcNow; 

            return Result.Success();
        }

        public Result UpdateLocation(Address newLocation, DateTime utcNow)
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
            LastModifiedAt = utcNow; 

            return Result.Success();
        }



        // ===== Admin Override Methods (bypass Draft-only restriction) ===

        public Result AdminUpdateName(string newName, DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(newName))
                return Result.Failure(EventErrors.NameCannotBeEmpty());

            if (newName.Length > MAX_NAME_LENGTH)
                return Result.Failure(EventErrors.NameTooLong(MAX_NAME_LENGTH));

            var nameResult = EventName.Create(newName);
            if (nameResult.IsFailure)
                return Result.Failure(nameResult.Errors.ToArray());

            EventName = nameResult.Value;
            LastModifiedAt = utcNow;

            return Result.Success();
        }

        public Result AdminUpdateCapacity(int newCapacityValue, DateTime utcNow)
        {
            if (newCapacityValue < MIN_CAPACITY)
                return Result.Failure(EventErrors.InvalidTotalSeats(newCapacityValue));

            var allocatedTickets = _ticketTypes.Sum(t => t.Capacity);
            if (newCapacityValue < allocatedTickets)
                return Result.Failure(EventErrors.TotalSeatsCannotBeLessThanAllocatedTickets(
                    newCapacityValue, allocatedTickets));

            var oldCapacity = Capacity;
            Capacity = newCapacityValue;
            LastModifiedAt = utcNow;

            RaiseDomainEvent(new EventCapacityUpdatedEvent(
                Id, oldCapacity, newCapacityValue, utcNow));

            return Result.Success();
        }

        public Result AdminUpdateDate(DateTime newDate, DateTime utcNow)
        {
            if (newDate < utcNow)
                return Result.Failure(EventErrors.InvalidEventDate(newDate));

            Date = newDate;
            LastModifiedAt = utcNow;

            return Result.Success();
        }

        public Result AdminUpdateDescription(string newDescription, DateTime utcNow)
        {
            if (newDescription.Length > MAX_DESCRIPTION_LENGTH)
                return Result.Failure(EventErrors.DescriptionTooLong(MAX_DESCRIPTION_LENGTH));

            Description = newDescription;
            LastModifiedAt = utcNow;

            return Result.Success();
        }

        public Result AdminUpdateLocation(Address newLocation, DateTime utcNow)
        {
            if (newLocation == null || string.IsNullOrWhiteSpace(newLocation.City))
                return Result.Failure(EventErrors.LocationCannotBeEmpty());

            var locationResult = Address.Create(
                newLocation.Country,
                newLocation.City,
                newLocation.Street ?? string.Empty);
            if (locationResult.IsFailure)
                return Result.Failure(locationResult.Errors.ToArray());

            Location = locationResult.Value;
            LastModifiedAt = utcNow;

            return Result.Success();
        }

        public Result AdminAddTicketType(
            string name,
            Money price,
            int capacity,
            DateTime dateTime)
        {
            if (_ticketTypes.Count >= MAX_TICKET_TYPES)
                return Result.Failure(EventErrors.MaxTicketTypesReached(MAX_TICKET_TYPES));

            var remainingCapacity = Capacity - _ticketTypes.Sum(t => t.Capacity);

            var ticketResult = TicketType.Create(
                Id, name, price, capacity, dateTime, remainingCapacity);

            if (ticketResult.IsFailure)
                return Result.Failure(ticketResult.Errors.ToArray());

            _ticketTypes.Add(ticketResult.Value);
            LastModifiedAt = dateTime;

            RaiseDomainEvent(new TicketTypeAddedEvent(
                Id, ticketResult.Value.Id, name, price.Amount, capacity, dateTime));

            return Result.Success();
        }

        // ===== Photo Management =====================================

        public Result AddPhoto(EventPhoto photo, DateTime utcNow)
        {
            if (photo == null)
                return Result.Failure(Error.Validation("Event.PhotoNull", "Photo cannot be null."));

            if (_photos.Count >= MAX_PHOTOS)
                return Result.Failure(EventErrors.MaxPhotosReached(MAX_PHOTOS));

            _photos.Add(photo);

            if (_photos.Count == 1)
                photo.SetCover();

            LastModifiedAt = utcNow;

            RaiseDomainEvent(new EventPhotosUpdatedEvent(
                Id, "Added", _photos.Count, utcNow));

            return Result.Success();
        }

        public Result RemovePhoto(EventPhotoId photoId, DateTime utcNow)
        {
            var photo = _photos.FirstOrDefault(p => p.Id == photoId);
            if (photo is null)
                return Result.Failure(EventErrors.PhotoNotFound(photoId));

            _photos.Remove(photo);
            LastModifiedAt = utcNow;

            RaiseDomainEvent(new EventPhotosUpdatedEvent(
                Id, "Removed", _photos.Count, utcNow));

            return Result.Success();
        }

        public Result SetCoverPhoto(EventPhotoId photoId, DateTime utcNow)
        {
            var target = _photos.FirstOrDefault(p => p.Id == photoId);
            if (target is null)
                return Result.Failure(EventErrors.PhotoNotFound(photoId));

            foreach (var photo in _photos)
            {
                if (photo.Id == photoId)
                {
                    if (!photo.IsCover)
                    {
                        var setResult = photo.SetCover();
                        if (setResult.IsFailure)
                            return setResult;
                    }
                }
                else if (photo.IsCover)
                {
                    photo.RemoveCover();
                }
            }

            LastModifiedAt = utcNow;

            RaiseDomainEvent(new EventPhotosUpdatedEvent(
                Id, "CoverChanged", _photos.Count, utcNow));

            return Result.Success();
        }

        public Result UpdatePhotoCaption(EventPhotoId photoId, string? caption,
            DateTime utcNow)
        {
            var photo = _photos.FirstOrDefault(p => p.Id == photoId);
            if (photo is null)
                return Result.Failure(EventErrors.PhotoNotFound(photoId));

            var updateResult = photo.UpdateCaption(caption);
            if (updateResult.IsFailure)
                return updateResult;

            LastModifiedAt = utcNow;

            RaiseDomainEvent(new EventPhotosUpdatedEvent(
                Id, "CaptionUpdated", _photos.Count, utcNow));

            return Result.Success();
        }

        public Result ReorderPhotos(List<EventPhotoId> orderedIds,
            DateTime utcNow)
        {
            if (orderedIds == null || orderedIds.Count != _photos.Count)
                return Result.Failure(
                    Error.Validation("Event.InvalidPhotoOrder",
                        "The number of ordered IDs must match the number of photos."));

            var validOrder = orderedIds.All(id => _photos.Any(p => p.Id == id));
            if (!validOrder)
                return Result.Failure(
                    Error.Validation("Event.InvalidPhotoOrder",
                        "All photo IDs in the order list must belong to this event."));

            for (int i = 0; i < orderedIds.Count; i++)
            {
                var photo = _photos.First(p => p.Id == orderedIds[i]);
                photo.UpdateDisplayOrder(i);
            }

            LastModifiedAt = utcNow;

            RaiseDomainEvent(new EventPhotosUpdatedEvent(
                Id, "Reordered", _photos.Count, utcNow));

            return Result.Success();
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

        public bool HasAvailableSeats()
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
