using Domain.Common;
using Domain.Exceptions;
using Domain.Primitives;
using Domain.Aggregates.EventAggregate.ValueObject;
using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Aggregates.EventAggregate.Errors;

namespace Domain.Aggregates.EventAggregate
{
    public class Event : AggregateRoot<EventId>
    {
        public EventName Name { get; private set; } = null!;
        public EventCapacity Capacity { get; private set; } = null!;

        public DateTime Date { get; private set; }
        public Address Location { get; private set; } = null!;
        public string Description { get; private set; } = string.Empty;
        public bool IsPublished { get; private set; }
        public bool IsCancelled { get; private set; }

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
            Address.Create(location.Country, location.City, location.Street);
            Description = description;
            Capacity = capacity;
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


            var newEvent = new Event(
                EventId.CreateUnqiue(),
                nameResult.Value,
                date,
                Address.Create(location.Country, location.City, location.Street),
                description,
                capacityResult.Value);

            return Result<Event>.Success(newEvent);
        }

        public Result Publish()
        {
            if (Date < DateTime.UtcNow)
                return Result.Failure(EventErrors.InvalidEventDate(Date));

            if (IsCancelled)
                return Result.Failure(EventErrors.CannotPublishCancelledEvent());

            if (_ticketTypes.Count == 0)
                return Result.Failure(EventErrors.CannotPublishWithoutTicketTypes());

            IsPublished = true;
            return Result.Success();
        }

        public Result Cancel()
        {
            if (IsCancelled)

                return Result.Failure(EventErrors.AlreadyCancelled());

            if (IsPublished)

                return Result.Failure(EventErrors.CannotCancelPublishedEvent());

            IsCancelled = true;
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

            _ticketTypes.Add(TicketType.Create(this.Id, name, price, capacity));
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
            if (IsPublished)
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
            Address.Create(newLocation.Country, newLocation.City, newLocation.Street);
            return Result.Success();
        }

        public Result UpdateDate(DateTime newDate)
        {
            if (newDate < DateTime.UtcNow)
                return Result.Failure(EventErrors.InvalidEventDate(Date));

            Date = newDate;
            return Result.Success();
        }
    }
}
