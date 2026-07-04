
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Authentication
{
    public sealed class JwtSettings
    {
        public const string SectionName = "Jwt";
        [Required] public string Secret { get; init; } = string.Empty;
        [Required] public string Issuer { get; init; } = string.Empty;
        [Required] public string Audience { get; init; } = string.Empty;
        public int ExpiryMinutes { get; init; } = 60;
    }


}
