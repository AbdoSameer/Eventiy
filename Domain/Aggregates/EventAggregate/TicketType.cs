using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Primitives;
using System;

namespace Domain.Aggregates.EventAggregate
{
    public class TicketType : Entity<TicketTypeId>
    {

        public EventId EventId { get; private set; } = null!;
        public string Name { get; private set; } = string.Empty;
        public Money Price { get; private set; } = null!;
        public int Capacity { get; private set; }

        protected TicketType() : base(default!)
        {
        }

        private TicketType(TicketTypeId ticketTypeId, EventId eventId, string name, Money price, int capacity)
            : base(ticketTypeId)
        {
            if (eventId is null)
                throw new ArgumentNullException(nameof(eventId), "Event ID cannot be null.");

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Ticket name cannot be empty.", nameof(name));

            if (price is null || price.Amount <= 0)
                throw new ArgumentException("Price must be greater than 0.", nameof(price));

            if (capacity <= 0)
                throw new ArgumentException("Capacity must be greater than 0.", nameof(capacity));

            EventId = eventId;
            Name = name;
            Price = price;
            Capacity = capacity;
        }

        public static TicketType Create(EventId eventId, string name, Money price, int capacity)
        {
            return new TicketType(TicketTypeId.CreateUnqiue(), eventId, name, price, capacity);
        }

        public void UpdatePrice(Money newPrice)
        {
            if (newPrice is null || newPrice.Amount <= 0)
                throw new ArgumentException("Price must be greater than zero.", nameof(newPrice));

            Price = newPrice;
        }

        public void UpdateCapacity(int newCapacity)
        {
            if (newCapacity <= 0)
                throw new ArgumentException("Capacity must be greater than zero.", nameof(newCapacity));

            if (newCapacity < Capacity)
                throw new ArgumentException("New capacity cannot be less than current capacity.", nameof(newCapacity));

            Capacity = newCapacity;
        }

        public void UpdateName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("Name cannot be empty.", nameof(newName));

            Name = newName;
        }
    }
}