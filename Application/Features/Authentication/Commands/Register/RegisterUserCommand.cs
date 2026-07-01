using Application.Abstractions.Messaging;
using Application.Features.Authentication.Responses;

namespace Application.Features.Authentication.Commands.Register
{
    public sealed record RegisterUserCommand(
        string FirstName,
        string LastName,
        string Email,
        string Password,
        string Role = "Attendee"   // Default Role
    ) : ICommand<AuthResponse>;

}
