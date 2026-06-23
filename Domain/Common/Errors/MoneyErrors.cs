// Domain/Common/MoneyErrors.cs
namespace Domain.Common
{
    public static class MoneyErrors
    {
        public static Error AmountCannotBeNegative()
            => Error.Validation(
                "Money.AmountCannotBeNegative",
                "Amount cannot be negative.");

        public static Error CurrencyCannotBeEmpty()
            => Error.Validation(
                "Money.CurrencyCannotBeEmpty",
                "Currency cannot be empty.");

        public static Error InvalidCurrencyLength()
            => Error.Validation(
                "Money.InvalidCurrencyLength",
                "Currency must be a 3-character ISO code.");

        public static Error InvalidCurrencyFormat()
            => Error.Validation(
                "Money.InvalidCurrencyFormat",
                "Currency must contain only letters.");



            // =========================================================================
            // ===== Arithmetic Errors =================================================
            // =========================================================================

            public static Error CannotAddNullMoney()
                => Error.Validation(
                    "Money.CannotAddNull",
                    "Cannot add null money.");

            public static Error CannotSubtractNullMoney()
                => Error.Validation(
                    "Money.CannotSubtractNull",
                    "Cannot subtract null money.");

            public static Error CurrencyMismatch(string currency1, string currency2)
                => Error.Validation(
                    "Money.CurrencyMismatch",
                    $"Cannot perform operation with different currencies: {currency1} and {currency2}.");

            public static Error NegativeResult()
                => Error.Validation(
                    "Money.NegativeResult",
                    "Cannot subtract to a negative amount.");

            public static Error NegativeMultiplier()
                => Error.Validation(
                    "Money.NegativeMultiplier",
                    "Cannot multiply by a negative number.");

            public static Error InvalidDivisor()
                => Error.Validation(
                    "Money.InvalidDivisor",
                    "Divisor must be greater than zero.");

            public static Error CannotDivideByZero()
                => Error.Validation(
                    "Money.DivideByZero",
                    "Cannot divide by zero.");

            // =========================================================================
            // ===== Comparison Errors =================================================
            // =========================================================================

            public static Error CannotCompareDifferentCurrencies(string currency1, string currency2)
                => Error.Validation(
                    "Money.CannotCompareCurrencies",
                    $"Cannot compare money with different currencies: {currency1} and {currency2}.");
        
    }
}
