using Application.Abstractions.Messaging;
using Application.Features.Authentication.Responses;


namespace Application.Features.Authentication.Commands.Login
{
    public sealed record LoginCommand(
        string Email,
        string Password
    ) : ICommand<AuthResponse>;

}
