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

        public static Result<TicketType> Create(EventId eventId, string name, Money price, int capacity)
        {
            if (eventId is null)
                return Result<TicketType>.Failure("Event ID cannot be null.");
            if (string.IsNullOrWhiteSpace(name))
                return Result<TicketType>.Failure("Name cannot be empty.");
            if (price is null || price.Amount <= 0)
                return Result<TicketType>.Failure("Price must be greater than zero.");
            if (capacity <= 0)
                return Result<TicketType>.Failure("Capacity must be greater than zero.");

            var priceResult = Money.Create(price.Amount, price.Currency);

            if (priceResult.IsFailure)
                return Result<TicketType>.Failure(priceResult.Error);

            return Result<TicketType>.Success(new TicketType(
                eventId,
                TicketTypeId.CreateUnqiue(),
                name,
                priceResult.Value!,
                capacity));
        }

        public Result UpdatePrice(Money newPrice)
        {
            if (newPrice is null || newPrice.Amount <= 0)
                return Result<TicketType>.Failure("Price must be greater than zero.");

            Price = newPrice;
            return Result.Success();
        }

        public Result UpdateCapacity(int newCapacity)
        {
            if (newCapacity <= 0)
                return Result.Failure("Capacity must be greater than zero.");
            if (newCapacity < Capacity)
                return Result.Failure("Capacity cannot be less than the current capacity.");
            Capacity = newCapacity;
            
            return Result.Success();
        }

        public Result UpdateName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            return Result.Failure("Name cannot be empty.");
            Name = newName;
            return Result.Success();
        }
    }
}