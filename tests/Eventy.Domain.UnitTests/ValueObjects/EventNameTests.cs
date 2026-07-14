using Domain.Aggregates.EventAggregate.ValueObject;
using FluentAssertions;
using Xunit;

namespace Eventy.Domain.UnitTests.ValueObjects;

public class EventNameTests
{
    [Theory]
    [InlineData("Cairo Music Festival", true)]
    [InlineData("Tech Summit 2026", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    public void Create_ShouldValidateName(string? input, bool shouldSucceed)
    {
        var result = EventName.Create(input!);

        result.IsSuccess.Should().Be(shouldSucceed);
    }
}
