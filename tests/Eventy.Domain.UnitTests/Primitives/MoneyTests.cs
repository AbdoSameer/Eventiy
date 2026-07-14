using Domain.Common;
using Domain.Primitives;
using FluentAssertions;
using Xunit;

namespace Eventy.Domain.UnitTests.Primitives;

public class MoneyTests
{
    [Fact]
    public void Create_WithPositiveAmount_ShouldSucceed()
    {
        var result = Money.Create(100.50m, "EGP");

        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(100.50m);
        result.Value.Currency.Should().Be("EGP");
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldReturnFailure()
    {
        var result = Money.Create(-10m, "EGP");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Zero_ShouldReturnMoneyWithZeroAmount()
    {
        var zero = Money.Zero("EGP").Value;

        zero.Amount.Should().Be(0m);
        zero.Currency.Should().Be("EGP");
    }

    [Fact]
    public void Add_TwoMoneyValues_ShouldReturnSum()
    {
        var a = Money.FromDecimal(50m, "EGP").Value;
        var b = Money.FromDecimal(30m, "EGP").Value;

        var sum = a.Add(b).Value;

        sum.Amount.Should().Be(80m);
    }

    [Fact]
    public void Subtract_ShouldReturnDifference()
    {
        var a = Money.FromDecimal(100m, "EGP").Value;
        var b = Money.FromDecimal(40m, "EGP").Value;

        var result = a.Subtract(b).Value;

        result.Amount.Should().Be(60m);
    }

    [Fact]
    public void Multiply_ByScalar_ShouldScaleAmount()
    {
        var money = Money.FromDecimal(50m, "EGP").Value;

        var result = money.Multiply(3).Value;

        result.Amount.Should().Be(150m);
    }

    [Fact]
    public void Percentage_OfMoney_ShouldCalculateCorrectly()
    {
        var money = Money.FromDecimal(200m, "EGP").Value;

        var tenPercent = money.Percentage(10).Value;

        tenPercent.Amount.Should().Be(20m);
    }
}
