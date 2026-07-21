using Domain.Aggregates.UserAggregate;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using FluentAssertions;
using Xunit;

namespace Eventy.Domain.UnitTests.Aggregates.UserAggregate;

public class UserTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;
    private static readonly Email DefaultEmail = Email.Create("test@example.com").Value;

    private User CreateValidUser()
    {
        var result = User.Create("John", "Doe", DefaultEmail, "hash", Role.Attendee, UtcNow);
        return result.Value;
    }

    #region IssueRefreshToken

    [Fact]
    public void IssueRefreshToken_ShouldRaiseRefreshTokenIssuedEvent()
    {
        var user = CreateValidUser();

        user.IssueRefreshToken("token-hash-1", UtcNow.AddDays(7), UtcNow);

        user.DomainEvents.Should().ContainSingle(e =>
            e.Name == "RefreshTokenIssuedEvent");
    }

    [Fact]
    public void IssueRefreshToken_ShouldAddTokenToCollection()
    {
        var user = CreateValidUser();

        user.IssueRefreshToken("token-hash-1", UtcNow.AddDays(7), UtcNow);

        user.RefreshTokens.Should().HaveCount(1);
        user.RefreshTokens.First().TokenHash.Should().Be("token-hash-1");
    }

    #endregion

    #region RevokeRefreshToken

    [Fact]
    public void RevokeRefreshToken_WhenTokenExists_ShouldRaiseRefreshTokenRevokedEvent()
    {
        var user = CreateValidUser();
        user.IssueRefreshToken("token-hash-1", UtcNow.AddDays(7), UtcNow);
        user.ClearDomainEvents();

        var result = user.RevokeRefreshToken("token-hash-1", UtcNow);

        result.IsSuccess.Should().BeTrue();
        user.DomainEvents.Should().ContainSingle(e =>
            e.Name == "RefreshTokenRevokedEvent");
    }

    [Fact]
    public void RevokeRefreshToken_WhenTokenNotFound_ShouldReturnFailure()
    {
        var user = CreateValidUser();

        var result = user.RevokeRefreshToken("nonexistent", UtcNow);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RevokeRefreshToken_ShouldRevokeToken()
    {
        var user = CreateValidUser();
        user.IssueRefreshToken("token-hash-1", UtcNow.AddDays(7), UtcNow);

        user.RevokeRefreshToken("token-hash-1", UtcNow);

        user.RefreshTokens.First().IsActiveAt(UtcNow).Should().BeFalse();
    }

    #endregion

    #region RevokeAllRefreshTokens

    [Fact]
    public void RevokeAllRefreshTokens_WhenActiveTokensExist_ShouldRaiseAllRefreshTokensRevokedEvent()
    {
        var user = CreateValidUser();
        user.IssueRefreshToken("token-hash-1", UtcNow.AddDays(7), UtcNow);
        user.IssueRefreshToken("token-hash-2", UtcNow.AddDays(7), UtcNow);
        user.ClearDomainEvents();

        user.RevokeAllRefreshTokens(UtcNow);

        user.DomainEvents.Should().ContainSingle(e =>
            e.Name == "AllRefreshTokensRevokedEvent");
    }

    [Fact]
    public void RevokeAllRefreshTokens_WhenNoActiveTokens_ShouldNotRaiseEvent()
    {
        var user = CreateValidUser();
        user.ClearDomainEvents();

        user.RevokeAllRefreshTokens(UtcNow);

        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RevokeAllRefreshTokens_ShouldRevokeAllActiveTokens()
    {
        var user = CreateValidUser();
        user.IssueRefreshToken("token-hash-1", UtcNow.AddDays(7), UtcNow);
        user.IssueRefreshToken("token-hash-2", UtcNow.AddDays(7), UtcNow);

        user.RevokeAllRefreshTokens(UtcNow);

        user.RefreshTokens.Should().OnlyContain(t => !t.IsActiveAt(UtcNow));
    }

    #endregion
}
