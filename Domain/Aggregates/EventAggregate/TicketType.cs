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
        private TicketType(TicketTypeId ticketTypeId) : base(ticketTypeId)
        {

        }

        private TicketType(EventId eventId, TicketTypeId ticketTypeId, string name, Money price, int capacity)
            : base(ticketTypeId)
        {
            EventId = eventId;
            Name = name;
            Price = price;
            Capacity = capacity;
        }

        public Result<TicketType> Create(EventId eventId, string name, Money price, int capacity)
        {
            if (eventId is null)
                Result<TicketType>.Failure("Event ID cannot be null.");
            if (string.IsNullOrWhiteSpace(name))
                Result<TicketType>.Failure("Name cannot be empty.");
            if (price is null || price.Amount <= 0)
                Result<TicketType>.Failure("Price must be greater than zero.");
            if (capacity <= 0)
                Result<TicketType>.Failure("Capacity must be greater than zero.");

            var priceResult = Money.Create(price.Amount, price.Currency);

            if (priceResult.IsFailure)
                return Result<TicketType>.Failure(priceResult.Error);

            return Result<TicketType>.Success(new TicketType(
                eventId!,
                TicketTypeId.CreateUnqiue(),
                name,
                price!,
                capacity));
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