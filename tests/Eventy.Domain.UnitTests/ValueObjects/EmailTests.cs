using Domain.Aggregates.UserAggregate.ValueObject;
using FluentAssertions;
using Xunit;

namespace Eventy.Domain.UnitTests.ValueObjects;

public class EmailTests
{
    [Theory]
    [InlineData("user@example.com", true)]
    [InlineData("test.user@domain.org", true)]
    [InlineData("admin@eventy.com", true)]
    [InlineData("", false)]
    [InlineData("not-an-email", false)]
    [InlineData("@missing-local.com", false)]
    [InlineData("spaces in@email.com", false)]
    public void Create_ShouldValidateFormat(string input, bool shouldSucceed)
    {
        var result = Email.Create(input);

        result.IsSuccess.Should().Be(shouldSucceed);
    }

    [Fact]
    public void Value_Equality_TwoIdenticalEmails_ShouldBeEqual()
    {
        var email1 = Email.Create("user@example.com").Value;
        var email2 = Email.Create("user@example.com").Value;

        email1.Should().Be(email2);
        email1.GetHashCode().Should().Be(email2.GetHashCode());
    }
}
