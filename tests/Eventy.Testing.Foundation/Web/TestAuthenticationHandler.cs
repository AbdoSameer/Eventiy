using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eventy.Testing.Foundation.Web;

/// <summary>
/// Deterministic test authentication — no real JWT tokens needed.
/// Override <see cref="ConfigureTestClaims"/> in fixtures to set per-test identities.
/// </summary>
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Support per-request UserId override via X-Test-UserId header.
        // If absent, falls back to DefaultUserId — preserving backward compatibility.
        var rawUserId = Request.Headers["X-Test-UserId"].FirstOrDefault();
        var userId = rawUserId is not null && Guid.TryParse(rawUserId, out var parsed)
            ? parsed
            : TestUsers.DefaultUserId;

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, $"Test User {userId}"),
            new Claim(ClaimTypes.Email, $"user-{userId}@eventy.com"),
            new Claim(ClaimTypes.Role, "Attendee"),
        }, "Test");

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Pre-configured test users for common scenarios.
/// </summary>
public static class TestUsers
{
    public static readonly Guid DefaultUserId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AdminUserId = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid OrganizerUserId = new("33333333-3333-3333-3333-333333333333");

    public static ClaimsPrincipal Default => CreatePrincipal(DefaultUserId, "Test User", "test@eventy.com", "Attendee");
    public static ClaimsPrincipal Admin => CreatePrincipal(AdminUserId, "Admin User", "admin@eventy.com", "Admin");
    public static ClaimsPrincipal Organizer => CreatePrincipal(OrganizerUserId, "Organizer User", "organizer@eventy.com", "Organizer");

    public static ClaimsPrincipal CreatePrincipal(Guid userId, string name, string email, string role) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role),
        }, "Test"));
}
