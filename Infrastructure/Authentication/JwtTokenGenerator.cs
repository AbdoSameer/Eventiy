using Application.Abstractions.Security;
using Domain.Aggregates.UserAggregate;
using Domain.Common;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Infrastructure.Authentication
{
    internal sealed class JwtTokenGenerator(
        IOptions<JwtSettings> jwtOptions,
        IDateTimeProvider dateTimeProvider) : IJwtTokenGenerator
    {
        private readonly JwtSettings _settings = jwtOptions.Value;

        public (string Token, DateTime ExpiresAt) GenerateToken(User user)
        {
            var expiresAt = dateTimeProvider.UtcNow.AddMinutes(_settings.ExpiryMinutes);

            var claims = new[]
            {
            // NameIdentifier هو ما يقرأه ICurrentUserService لاحقاً
            new Claim(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
            new Claim(ClaimTypes.Email,          user.Email.Value),
            new Claim(ClaimTypes.Role,           user.Role.Value),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // JWT ID للـ Revocation
        };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }
    }

}
