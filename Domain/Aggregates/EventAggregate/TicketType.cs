using Domain.Aggregates.EventAggregate.ValueObject;
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
        public int ReservedCount { get; private set; }
        public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
        public DateTime CreatedAt { get; private set; }
        public DateTime? LastModifiedAt { get; private set; }

        // Computed Properties

        public int AvailableCount => Capacity - SoldCount - ReservedCount;

        public double OccupancyRate => Capacity > 0
            ? (double)SoldCount / Capacity * 100
            : 0;

        public double ReservationRate => Capacity > 0
            ? (double)ReservedCount / Capacity * 100
            : 0;

        public int UnavailableCount => SoldCount + ReservedCount;

        // Constructors

        protected TicketType() : base(default!)
        {
        }

        private TicketType(
            EventId eventId,
            TicketTypeId ticketTypeId,
            string name,
            Money price,
            int capacity,
            DateTime createdAt) : base(ticketTypeId)
        {
            EventId = eventId;
            TicketTypeName = name;
            Price = price;
            Capacity = capacity;
            SoldCount = 0;
            ReservedCount = 0;
            CreatedAt = createdAt;
            RowVersion = Array.Empty<byte>();
        }

        public static Result<TicketType> Create(
            EventId eventId,
            string name,
            Money price,
            int capacity,
            IDateTimeProvider dateTimeProvider,
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
                capacity,
                dateTimeProvider.UtcNow); 

            return Result<TicketType>.Success(ticketType);
        }

        // Update Methods

        public Result UpdatePrice(Money newPrice, IDateTimeProvider dateTimeProvider)
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
            LastModifiedAt = dateTimeProvider.UtcNow; 

            return Result.Success();
        }

        public Result UpdateCapacity(int newCapacity, IDateTimeProvider dateTimeProvider, int? remainingEventCapacity = null)
        {
            // Validate
            if (newCapacity < MIN_CAPACITY)
                return Result.Failure(TicketTypeErrors.CapacityMustBeGreaterThanZero());

            var totalOccupied = SoldCount + ReservedCount;
            if (newCapacity < totalOccupied)
                return Result.Failure(TicketTypeErrors.CannotReduceCapacityBelowOccupied(
                    totalOccupied, newCapacity));

            if (remainingEventCapacity.HasValue && newCapacity > remainingEventCapacity.Value)
                return Result.Failure(TicketTypeErrors.CapacityExceedsEventRemainingCapacity(
                    remainingEventCapacity.Value));

            // Check if capacity changed
            if (Capacity == newCapacity)
                return Result.Success();

            // Update
            Capacity = newCapacity;
            LastModifiedAt = dateTimeProvider.UtcNow; 

            return Result.Success();
        }

        public Result UpdateName(string newName, IDateTimeProvider dateTimeProvider)
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
            LastModifiedAt = dateTimeProvider.UtcNow; 

            return Result.Success();
        }

        // Seat Management Methods

        public Result ReserveSeats(int quantity, IDateTimeProvider dateTimeProvider)
        {
            if (quantity <= 0)
                return Result.Failure(TicketTypeErrors.QuantityMustBeGreaterThanZero());

            if (quantity > AvailableCount)
                return Result.Failure(TicketTypeErrors.InsufficientAvailableSeats(
                    quantity, AvailableCount));

            ReservedCount += quantity;
            LastModifiedAt = dateTimeProvider.UtcNow;
            return Result.Success();
        }

        public Result ReleaseSeats(int quantity, IDateTimeProvider dateTimeProvider)
        {
            if (quantity <= 0)
                return Result.Failure(TicketTypeErrors.QuantityMustBeGreaterThanZero());

            if (quantity > ReservedCount)
                return Result.Failure(TicketTypeErrors.CannotReleaseMoreThanReserved(
                    quantity, ReservedCount));

            ReservedCount -= quantity;
            LastModifiedAt = dateTimeProvider.UtcNow; 

            return Result.Success();
        }

        public Result ConfirmReservation(int quantity, IDateTimeProvider dateTimeProvider)
        {
            if (quantity <= 0)
                return Result.Failure(TicketTypeErrors.QuantityMustBeGreaterThanZero());

            if (quantity > ReservedCount)
                return Result.Failure(TicketTypeErrors.CannotConfirmMoreThanReserved(
                    quantity, ReservedCount));

            if (SoldCount + quantity > Capacity)
                return Result.Failure(TicketTypeErrors.CapacityExceeded(
                    Capacity, SoldCount, quantity));

            SoldCount += quantity;
            ReservedCount -= quantity;
            LastModifiedAt = dateTimeProvider.UtcNow; 

            return Result.Success();
        }

        public Result SellDirect(int quantity, IDateTimeProvider dateTimeProvider)
        {
            if (quantity <= 0)
                return Result.Failure(TicketTypeErrors.QuantityMustBeGreaterThanZero());

            if (quantity > AvailableCount) 
                return Result.Failure(TicketTypeErrors.InsufficientAvailableSeats(
                    quantity, AvailableCount));

            SoldCount += quantity;
            LastModifiedAt = dateTimeProvider.UtcNow;
            return Result.Success();
        }

        public Result RefundSeats(int quantity, IDateTimeProvider dateTimeProvider)
        {
            if (quantity <= 0)
                return Result.Failure(TicketTypeErrors.QuantityMustBeGreaterThanZero());

            if (quantity > SoldCount)
                return Result.Failure(TicketTypeErrors.CannotRefundMoreThanSold(
                    quantity, SoldCount));

            SoldCount -= quantity;
            LastModifiedAt = dateTimeProvider.UtcNow; 

            return Result.Success();
        }

        // Remove Method

        public Result Remove()
        {
            if (SoldCount > 0)
                return Result.Failure(TicketTypeErrors.CannotRemoveTicketTypeWithBookings(SoldCount));

            if (ReservedCount > 0)
                return Result.Failure(TicketTypeErrors.CannotRemoveTicketTypeWithReservations(ReservedCount));

            return Result.Success();
        }

        // Query Methods

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

        public double GetReservationRate()
        {
            if (Capacity == 0) return 0;
            return (double)ReservedCount / Capacity * 100;
        }

        public bool IsEmpty()
        {
            return SoldCount == 0;
        }

        public bool HasNoReservations()
        {
            return ReservedCount == 0;
        }

        public bool IsFullyBooked()
        {
            return AvailableCount <= 0;

        }

        public bool IsReallyFullyBooked()
        {
            return SoldCount + ReservedCount >= Capacity;
        }

        public override string ToString()
        {
            return $"{TicketTypeName} - {Price.Amount} {Price.Currency} ({AvailableCount} available, {ReservedCount} reserved)";
        }
    }
}