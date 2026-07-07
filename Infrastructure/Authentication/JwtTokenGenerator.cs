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
    internal sealed class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly JwtSettings _settings;
        private readonly TimeProvider _dateTimeProvider;

        public JwtTokenGenerator(
            IOptions<JwtSettings> jwtOptions,
            TimeProvider dateTimeProvider)
        {
            _settings = jwtOptions.Value;
            _dateTimeProvider = dateTimeProvider;
        }

        public (string Token, DateTime ExpiresAt) GenerateToken(User user)
        {
            var expiresAt = _dateTimeProvider.GetUtcNow().UtcDateTime.AddMinutes(_settings.ExpiryMinutes);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
                new Claim(ClaimTypes.Email,          user.Email.Value),
                new Claim(ClaimTypes.Role,           user.Role.Value),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
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
