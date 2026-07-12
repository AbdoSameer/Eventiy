using Domain.Aggregates.UserAggregate;

namespace Application.Abstractions.Security
{
    public interface IJwtTokenGenerator
    {
        (string Token, DateTime ExpiresAt) GenerateToken(User user);
        string GenerateRefreshToken();
        string HashToken(string token);
    }
}
