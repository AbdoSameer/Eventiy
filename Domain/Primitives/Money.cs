using Domain.Common;

namespace Domain.Primitives
{
    public class Money : ValueObjectBase
    {
        public decimal Amount { get; }
        public string Currency { get; }


        private Money(decimal amount, string currency)
        {
            Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
            Currency = currency.ToUpperInvariant();
        }


        public static Result<Money> Create(decimal amount, string currency)
        {
            // Validate amount
            if (amount < 0)
                return Result<Money>.Failure(MoneyErrors.AmountCannotBeNegative());

            // Validate currency
            if (string.IsNullOrWhiteSpace(currency))
                return Result<Money>.Failure(MoneyErrors.CurrencyCannotBeEmpty());

            if (currency.Length != 3)
                return Result<Money>.Failure(MoneyErrors.InvalidCurrencyLength());

            // Check if currency contains only letters
            if (!currency.All(char.IsLetter))
                return Result<Money>.Failure(MoneyErrors.InvalidCurrencyLength());

            return Result<Money>.Success(new Money(amount, currency));
        }

        public static Money Zero(string currency)
        {
            var result = Create(0, currency);
            if (result.IsFailure)
                throw new InvalidOperationException($"Cannot create zero money: {result.Errors.First().Message}");
            return result.Value;
        }

        public static Money FromDecimal(decimal amount, string currency)
        {
            var result = Create(amount, currency);
            if (result.IsFailure)
                throw new InvalidOperationException($"Cannot create money: {result.Errors.First().Message}");
            return result.Value;
        }

        public Result<Money> Add(Money other)
        {
            if (other is null)
                return Result<Money>.Failure(MoneyErrors.CannotAddNullMoney());

            if (Currency != other.Currency)
                return Result<Money>.Failure(MoneyErrors.CurrencyMismatch(Currency, other.Currency));

            return Result<Money>.Success(new Money(Amount + other.Amount, Currency));
        }

        public Result<Money> Subtract(Money other)
        {
            if (other is null)
                return Result<Money>.Failure(MoneyErrors.CannotSubtractNullMoney());

            if (Currency != other.Currency)
                return Result<Money>.Failure(MoneyErrors.CurrencyMismatch(Currency, other.Currency));

            var result = Amount - other.Amount;
            if (result < 0)
                return Result<Money>.Failure(MoneyErrors.NegativeResult());

            return Result<Money>.Success(new Money(result, Currency));
        }

        public Result<Money> Multiply(decimal multiplier)
        {
            if (multiplier < 0)
                return Result<Money>.Failure(MoneyErrors.NegativeMultiplier());

            return Result<Money>.Success(new Money(Amount * multiplier, Currency));
        }

        public Result<Money> Divide(decimal divisor)
        {
            if (divisor <= 0)
                return Result<Money>.Failure(MoneyErrors.InvalidDivisor());

            return Result<Money>.Success(new Money(Amount / divisor, Currency));
        }

        public Money Round(int decimals = 2)
        {
            var rounded = Math.Round(Amount, decimals, MidpointRounding.AwayFromZero);
            return new Money(rounded, Currency);
        }

        public bool IsZero() => Amount == 0;
        public bool IsPositive() => Amount > 0;
        public bool IsNegative() => Amount < 0;
        public bool IsSameCurrency(Money other) => other is not null && Currency == other.Currency;

        public Money Percentage(decimal percentage)
        {
            if (percentage < 0 || percentage > 100)
                throw new ArgumentException("Percentage must be between 0 and 100.");

            return new Money(Amount * (percentage / 100), Currency);
        }

        public Money AddPercentage(decimal percentage)
        {
            if (percentage < 0 || percentage > 100)
                throw new ArgumentException("Percentage must be between 0 and 100.");

            return new Money(Amount * (1 + percentage / 100), Currency);
        }

        public Money SubtractPercentage(decimal percentage)
        {
            if (percentage < 0 || percentage > 100)
                throw new ArgumentException("Percentage must be between 0 and 100.");

            return new Money(Amount * (1 - percentage / 100), Currency);
        }

        public bool IsGreaterThan(Money other)
        {
            if (other is null) return false;
            if (Currency != other.Currency)
                throw new InvalidOperationException($"Cannot compare money with different currencies: {Currency} and {other.Currency}.");
            return Amount > other.Amount;
        }

        public bool IsLessThan(Money other)
        {
            if (other is null) return false;
            if (Currency != other.Currency)
                throw new InvalidOperationException($"Cannot compare money with different currencies: {Currency} and {other.Currency}.");
            return Amount < other.Amount;
        }

        public bool IsGreaterThanOrEqual(Money other)
        {
            if (other is null) return false;
            if (Currency != other.Currency)
                throw new InvalidOperationException($"Cannot compare money with different currencies: {Currency} and {other.Currency}.");
            return Amount >= other.Amount;
        }

        public bool IsLessThanOrEqual(Money other)
        {
            if (other is null) return false;
            if (Currency != other.Currency)
                throw new InvalidOperationException($"Cannot compare money with different currencies: {Currency} and {other.Currency}.");
            return Amount <= other.Amount;
        }

        public bool IsEqualTo(Money other)
        {
            if (other is null) return false;
            return Currency == other.Currency && Amount == other.Amount;
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }

        public override string ToString()
        {
            return $"{Currency} {Amount:F2}";
        }

        public string ToString(string format)
        {
            return $"{Currency} {Amount.ToString(format)}";
        }

        public string ToStringWithoutCurrency()
        {
            return Amount.ToString("F2");
        }

        public string ToCurrencyFormat()
        {
            return Amount.ToString("C", new System.Globalization.CultureInfo("en-US"));
        }

        public static Money operator +(Money left, Money right)
        {
            if (left is null || right is null)
                throw new ArgumentNullException("Cannot add null money.");

            if (left.Currency != right.Currency)
                throw new InvalidOperationException($"Cannot add money with different currencies: {left.Currency} and {right.Currency}.");

            return new Money(left.Amount + right.Amount, left.Currency);
        }

        public static Money operator -(Money left, Money right)
        {
            if (left is null || right is null)
                throw new ArgumentNullException("Cannot subtract null money.");

            if (left.Currency != right.Currency)
                throw new InvalidOperationException($"Cannot subtract money with different currencies: {left.Currency} and {right.Currency}.");

            if (left.Amount - right.Amount < 0)
                throw new InvalidOperationException("Cannot subtract to a negative amount.");

            return new Money(left.Amount - right.Amount, left.Currency);
        }

        public static Money operator *(Money money, decimal multiplier)
        {
            if (money is null)
                throw new ArgumentNullException("Cannot multiply null money.");

            if (multiplier < 0)
                throw new InvalidOperationException("Cannot multiply by a negative number.");

            return new Money(money.Amount * multiplier, money.Currency);
        }

        public static Money operator /(Money money, decimal divisor)
        {
            if (money is null)
                throw new ArgumentNullException("Cannot divide null money.");

            if (divisor <= 0)
                throw new InvalidOperationException("Divisor must be greater than zero.");

            return new Money(money.Amount / divisor, money.Currency);
        }

        public static bool operator >(Money left, Money right)
        {
            if (left is null || right is null) return false;
            if (left.Currency != right.Currency)
                throw new InvalidOperationException($"Cannot compare money with different currencies: {left.Currency} and {right.Currency}.");
            return left.Amount > right.Amount;
        }

        public static bool operator <(Money left, Money right)
        {
            if (left is null || right is null) return false;
            if (left.Currency != right.Currency)
                throw new InvalidOperationException($"Cannot compare money with different currencies: {left.Currency} and {right.Currency}.");
            return left.Amount < right.Amount;
        }

        public static bool operator >=(Money left, Money right)
        {
            if (left is null || right is null) return false;
            if (left.Currency != right.Currency)
                throw new InvalidOperationException($"Cannot compare money with different currencies: {left.Currency} and {right.Currency}.");
            return left.Amount >= right.Amount;
        }

        public static bool operator <=(Money left, Money right)
        {
            if (left is null || right is null) return false;
            if (left.Currency != right.Currency)
                throw new InvalidOperationException($"Cannot compare money with different currencies: {left.Currency} and {right.Currency}.");
            return left.Amount <= right.Amount;
        }

        public static bool operator ==(Money left, Money right)
        {
            if (ReferenceEquals(left, null) && ReferenceEquals(right, null))
                return true;

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;

            return left.Currency == right.Currency && left.Amount == right.Amount;
        }

        public static bool operator !=(Money left, Money right)
        {
            return !(left == right);
        }

        public static implicit operator decimal(Money money) => money.Amount;
        public static explicit operator Money(decimal amount) => new Money(amount, "USD");

        public override bool Equals(object? obj)
        {
            if (obj is not Money other)
                return false;

            return Currency == other.Currency && Amount == other.Amount;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Currency, Amount);
        }
    }
}