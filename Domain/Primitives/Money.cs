using Domain.Common;
using Domain.Errors;

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

        // ===== FACTORY METHODS =====
        public static Result<Money> Create(decimal amount, string currency)
        {
            if (amount < 0)
                return Result<Money>.Failure(MoneyErrors.AmountCannotBeNegative());

            if (string.IsNullOrWhiteSpace(currency))
                return Result<Money>.Failure(MoneyErrors.CurrencyCannotBeEmpty());

            if (currency.Length != 3)
                return Result<Money>.Failure(MoneyErrors.InvalidCurrencyLength());

            if (!currency.All(char.IsLetter))
                return Result<Money>.Failure(MoneyErrors.InvalidCurrencyCode());

            return Result<Money>.Success(new Money(amount, currency));
        }

        public static Result<Money> Zero(string currency)
            => Create(0, currency);

        public static Result<Money> FromDecimal(decimal amount, string currency)
            => Create(amount, currency);

        // ===== ARITHMETIC OPERATIONS =====
        public Result<Money> Add(Money other)
        {
            if (other is null)
                return Result<Money>.Failure(MoneyErrors.CannotAddNullMoney());

            if (!IsSameCurrency(other))
                return Result<Money>.Failure(MoneyErrors.CurrencyMismatch(Currency, other.Currency));

            return Create(Amount + other.Amount, Currency);
        }

        public Result<Money> Subtract(Money other)
        {
            if (other is null)
                return Result<Money>.Failure(MoneyErrors.CannotSubtractNullMoney());

            if (!IsSameCurrency(other))
                return Result<Money>.Failure(MoneyErrors.CurrencyMismatch(Currency, other.Currency));

            var result = Amount - other.Amount;
            if (result < 0)
                return Result<Money>.Failure(MoneyErrors.NegativeResult());

            return Create(result, Currency);
        }

        public Result<Money> Multiply(decimal multiplier)
        {
            if (multiplier < 0)
                return Result<Money>.Failure(MoneyErrors.NegativeMultiplier());

            return Create(Amount * multiplier, Currency);
        }

        public Result<Money> Divide(decimal divisor)
        {
            if (divisor <= 0)
                return Result<Money>.Failure(MoneyErrors.InvalidDivisor());

            return Create(Amount / divisor, Currency);
        }

        public Result<Money> Percentage(decimal percentage)
        {
            if (percentage < 0 || percentage > 100)
                return Result<Money>.Failure(MoneyErrors.InvalidPercentage(percentage));

            return Create(Amount * (percentage / 100), Currency);
        }

        public Result<Money> AddPercentage(decimal percentage)
        {
            if (percentage < 0 || percentage > 100)
                return Result<Money>.Failure(MoneyErrors.InvalidPercentage(percentage));

            return Create(Amount * (1 + percentage / 100), Currency);
        }

        public Result<Money> SubtractPercentage(decimal percentage)
        {
            if (percentage < 0 || percentage > 100)
                return Result<Money>.Failure(MoneyErrors.InvalidPercentage(percentage));

            return Create(Amount * (1 - percentage / 100), Currency);
        }

        public Money Round(int decimals = 2)
        {
            var rounded = Math.Round(Amount, decimals, MidpointRounding.AwayFromZero);
            return new Money(rounded, Currency);
        }

        // ===== COMPARISON OPERATIONS =====
        public Result<bool> IsGreaterThan(Money other)
        {
            if (other is null)
                return Result<bool>.Failure(MoneyErrors.CannotCompareNullMoney());

            if (!IsSameCurrency(other))
                return Result<bool>.Failure(MoneyErrors.CurrencyMismatch(Currency, other.Currency));

            return Result<bool>.Success(Amount > other.Amount);
        }

        public Result<bool> IsLessThan(Money other)
        {
            if (other is null)
                return Result<bool>.Failure(MoneyErrors.CannotCompareNullMoney());

            if (!IsSameCurrency(other))
                return Result<bool>.Failure(MoneyErrors.CurrencyMismatch(Currency, other.Currency));

            return Result<bool>.Success(Amount < other.Amount);
        }

        public Result<bool> IsGreaterThanOrEqual(Money other)
        {
            if (other is null)
                return Result<bool>.Failure(MoneyErrors.CannotCompareNullMoney());

            if (!IsSameCurrency(other))
                return Result<bool>.Failure(MoneyErrors.CurrencyMismatch(Currency, other.Currency));

            return Result<bool>.Success(Amount >= other.Amount);
        }

        public Result<bool> IsLessThanOrEqual(Money other)
        {
            if (other is null)
                return Result<bool>.Failure(MoneyErrors.CannotCompareNullMoney());

            if (!IsSameCurrency(other))
                return Result<bool>.Failure(MoneyErrors.CurrencyMismatch(Currency, other.Currency));

            return Result<bool>.Success(Amount <= other.Amount);
        }

        public Result<bool> IsEqualTo(Money other)
        {
            if (other is null)
                return Result<bool>.Failure(MoneyErrors.CannotCompareNullMoney());

            return Result<bool>.Success(Currency == other.Currency && Amount == other.Amount);
        }

        // ===== HELPER METHODS =====
        public bool IsSameCurrency(Money other)
            => other is not null && Currency == other.Currency;

        public bool IsZero() => Amount == 0;
        public bool IsPositive() => Amount > 0;
        public bool IsNegative() => Amount < 0;

        // ===== CONVERSION =====
        public static implicit operator decimal(Money money) => money.Amount;


        public Result<string> ToCurrencyFormat()
        {
            try
            {
                return Result<string>.Success(
                    Amount.ToString("C", new System.Globalization.CultureInfo("en-US"))
                );
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(
                    Error.Failure("Money.FormatError", $"Failed to format currency: {ex.Message}")
                );
            }
        }

        // ===== VALUE OBJECT =====
        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }

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
    }
}