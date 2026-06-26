using Domain.Common;

namespace Domain.Errors
{
    public static class MoneyErrors
    {
        public static Error AmountCannotBeNegative()
            => Error.Validation("Money.AmountNegative", "Amount cannot be negative.");

        public static Error CurrencyCannotBeEmpty()
            => Error.Validation("Money.CurrencyEmpty", "Currency cannot be empty.");

        public static Error InvalidCurrencyCode()
            => Error.Validation("Money.InvalidCurrency", "Currency must be a valid 3-letter ISO code.");


        public static Error InvalidCurrencyLength()
            => Error.Validation("Money.InvalidCurrencyLength", "Currency must be exactly 3 characters.");


        public static Error CurrencyMismatch(string currency1, string currency2)
            => Error.Validation("Money.CurrencyMismatch",
                $"Cannot operate on different currencies: {currency1} and {currency2}.");

        public static Error CannotAddNullMoney()
            => Error.Validation("Money.CannotAddNull", "Cannot add null money.");

        public static Error CannotSubtractNullMoney()
            => Error.Validation("Money.CannotSubtractNull", "Cannot subtract null money.");

        public static Error CannotCompareNullMoney()
            => Error.Validation("Money.CannotCompareNull", "Cannot compare with null money.");

        public static Error NegativeResult()
            => Error.Validation("Money.NegativeResult", "Result would be negative.");

        public static Error NegativeMultiplier()
            => Error.Validation("Money.NegativeMultiplier", "Multiplier cannot be negative.");

        public static Error InvalidDivisor()
            => Error.Validation("Money.InvalidDivisor", "Divisor must be greater than zero.");

        public static Error InvalidPercentage(decimal percentage)
            => Error.Validation("Money.InvalidPercentage",
                $"Percentage must be between 0 and 100. Received: {percentage}.");
    }
}