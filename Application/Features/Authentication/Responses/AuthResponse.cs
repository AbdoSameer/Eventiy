
namespace Application.Features.Authentication.Responses
{
    public sealed record AuthResponse(
        Guid UserId,
        string Email,
        string Role,
        string Token,
        DateTime ExpiresAt
    );

}
