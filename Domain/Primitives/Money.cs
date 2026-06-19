using Domain.Common;

namespace Domain.Primitives;

public record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money() { }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency.ToUpper();
    }
    public static Result<Money> Create(decimal amount, string currency)
    {
        if (amount < 0)
            return Result<Money>.Failure("Amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            return Result<Money>.Failure("Currency must be a valid 3 character code.");

        return Result<Money>.Success(new Money(amount, currency));

    }
    public Result<Money> Add(Money other)
    {
        if (Currency != other.Currency)
            return Result<Money>.Failure("Cannot add money with different currencies.");

        return Result<Money>.Success(new Money(Amount + other.Amount, Currency));
    }

}