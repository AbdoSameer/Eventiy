using Domain.Common;

namespace Domain.Primitives;

public record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money() { }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency.ToUpper();
    }
    public static Result<Money> Create(decimal amount, string currency)
    {
        if (amount < 0)
        Result.Failure("Amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        Result.Failure("Currency must be a valid 3 character code.");

        return Result<Money>.Success(new Money(amount, currency));
        
    }
    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add amounts with different currencies.");

        return new Money(Amount + other.Amount, Currency);
    }
}