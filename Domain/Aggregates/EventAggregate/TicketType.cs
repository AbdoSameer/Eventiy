using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Common;
using Domain.Primitives;
using Domain.Errors;

namespace Domain.Aggregates.EventAggregate
{
    public class TicketType : Entity<TicketTypeId>
    {
        private const int MAX_NAME_LENGTH = 100;
        private const int MIN_CAPACITY = 1;
        private const decimal MIN_PRICE = 0;

        public EventId EventId { get; private set; } = null!;
        public string TicketTypeName { get; private set; } = string.Empty;
        public Money Price { get; private set; } = null!;
        public int Capacity { get; private set; }
        public int SoldCount { get; private set; }
        public int AvailableCount => Capacity - SoldCount;
        public bool IsActive => AvailableCount > 0;
        public bool IsAtFullCapacity => AvailableCount == 0;
        public DateTime CreatedAt { get; private set; }
        public DateTime? LastModifiedAt { get; private set; }


        protected TicketType() : base(default!)
        {
        }

        private TicketType(TicketTypeId ticketTypeId) : base(ticketTypeId)
        {
        }

        private TicketType(
            EventId eventId,
            TicketTypeId ticketTypeId,
            string name,
            Money price,
            int capacity) : base(ticketTypeId)
        {
            EventId = eventId;
            TicketTypeName = name;
            Price = price;
            Capacity = capacity;
            SoldCount = 0;
            CreatedAt = DateTime.UtcNow;
        }


        public static Result<TicketType> Create(
            EventId eventId,
            string name,
            Money price,
            int capacity,
            int? remainingEventCapacity = null)
        {
            // Validate EventId
            if (eventId is null)
                return Result<TicketType>.Failure(TicketTypeErrors.EventIdRequired());

            // Validate Name
            if (string.IsNullOrWhiteSpace(name))
                return Result<TicketType>.Failure(TicketTypeErrors.NameCannotBeEmpty());

            if (name.Length > MAX_NAME_LENGTH)
                return Result<TicketType>.Failure(TicketTypeErrors.NameTooLong(MAX_NAME_LENGTH));

            // Validate Price
            if (price is null)
                return Result<TicketType>.Failure(TicketTypeErrors.PriceCannotBeNull());

            if (price.Amount <= MIN_PRICE)
                return Result<TicketType>.Failure(TicketTypeErrors.PriceMustBeGreaterThanZero());

            if (string.IsNullOrWhiteSpace(price.Currency))
                return Result<TicketType>.Failure(TicketTypeErrors.InvalidCurrency());

            // Validate Capacity
            if (capacity < MIN_CAPACITY)
                return Result<TicketType>.Failure(TicketTypeErrors.CapacityMustBeGreaterThanZero());

            // Validate against remaining event capacity
            if (remainingEventCapacity.HasValue && capacity > remainingEventCapacity.Value)
                return Result<TicketType>.Failure(TicketTypeErrors.CapacityExceedsEventRemainingCapacity(
                    remainingEventCapacity.Value));

            // Create value objects
            var priceResult = Money.Create(price.Amount, price.Currency);
            if (priceResult.IsFailure)
                return Result<TicketType>.Failure(priceResult.Errors.ToArray());

            var ticketTypeIdResult = TicketTypeId.Create(Guid.NewGuid());
            if (ticketTypeIdResult.IsFailure)
                return Result<TicketType>.Failure(ticketTypeIdResult.Errors.ToArray());

            var ticketType = new TicketType(
                eventId,
                ticketTypeIdResult.Value,
                name.Trim(),
                priceResult.Value!,
                capacity);

            return Result<TicketType>.Success(ticketType);
        }

        public Result UpdatePrice(Money newPrice)
        {
            // Validate
            if (newPrice is null)
                return Result.Failure(TicketTypeErrors.PriceCannotBeNull());

            if (newPrice.Amount <= MIN_PRICE)
                return Result.Failure(TicketTypeErrors.PriceMustBeGreaterThanZero());

            if (string.IsNullOrWhiteSpace(newPrice.Currency))
                return Result.Failure(TicketTypeErrors.InvalidCurrency());

            // Check if price changed
            if (Price.Amount == newPrice.Amount && Price.Currency == newPrice.Currency)
                return Result.Success();

            // Update
            Price = newPrice;
            LastModifiedAt = DateTime.UtcNow;

            return Result.Success();
        }

        public Result UpdateCapacity(int newCapacity, int? remainingEventCapacity = null)
        {
            // Validate
            if (newCapacity < MIN_CAPACITY)
                return Result.Failure(TicketTypeErrors.CapacityMustBeGreaterThanZero());

            if (newCapacity < SoldCount)
                return Result.Failure(TicketTypeErrors.CannotReduceCapacityBelowSoldTickets(SoldCount));

            if (remainingEventCapacity.HasValue && newCapacity > remainingEventCapacity.Value)
                return Result.Failure(TicketTypeErrors.CapacityExceedsEventRemainingCapacity(
                    remainingEventCapacity.Value));

            // Check if capacity changed
            if (Capacity == newCapacity)
                return Result.Success();

            // Update
            Capacity = newCapacity;
            LastModifiedAt = DateTime.UtcNow;

            return Result.Success();
        }

        public Result UpdateName(string newName)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(newName))
                return Result.Failure(TicketTypeErrors.NameCannotBeEmpty());

            if (newName.Length > MAX_NAME_LENGTH)
                return Result.Failure(TicketTypeErrors.NameTooLong(MAX_NAME_LENGTH));

            // Check if name changed
            if (TicketTypeName == newName.Trim())
                return Result.Success();

            // Update
            TicketTypeName = newName.Trim();
            LastModifiedAt = DateTime.UtcNow;

            return Result.Success();
        }

        public Result ReserveSeats(int quantity)
        {
            // Validate
            if (quantity <= 0)
                return Result.Failure(TicketTypeErrors.QuantityMustBeGreaterThanZero());

            if (quantity > AvailableCount)
                return Result.Failure(TicketTypeErrors.InsufficientAvailableSeats(
                    quantity, AvailableCount));

            // Update
            SoldCount += quantity;
            LastModifiedAt = DateTime.UtcNow;

            return Result.Success();
        }

        public Result ReleaseSeats(int quantity)
        {
            // Validate
            if (quantity <= 0)
                return Result.Failure(TicketTypeErrors.QuantityMustBeGreaterThanZero());

            if (quantity > SoldCount)
                return Result.Failure(TicketTypeErrors.CannotReleaseMoreThanSold(quantity, SoldCount));

            // Update
            SoldCount -= quantity;
            LastModifiedAt = DateTime.UtcNow;

            return Result.Success();
        }

        public Result Remove()
        {
            // Cannot remove ticket type with existing bookings
            if (SoldCount > 0)
                return Result.Failure(TicketTypeErrors.CannotRemoveTicketTypeWithBookings(SoldCount));

            return Result.Success();
        }

        public bool HasAvailableSeats()
        {
            return AvailableCount > 0;
        }

        public bool CanAccommodate(int quantity)
        {
            return quantity > 0 && quantity <= AvailableCount;
        }

        public decimal CalculateTotalPrice(int quantity)
        {
            if (quantity <= 0) return 0;
            return Price.Amount * quantity;
        }

        public double GetOccupancyRate()
        {
            if (Capacity == 0) return 0;
            return (double)SoldCount / Capacity * 100;
        }

        public bool IsEmpty()
        {
            return SoldCount == 0;
        }

        public bool IsFullyBooked()
        {
            return SoldCount >= Capacity;
        }

        public override string ToString()
        {
            return $"{TicketTypeName} - {Price.Amount} {Price.Currency} ({AvailableCount} available)";
        }
    }
}