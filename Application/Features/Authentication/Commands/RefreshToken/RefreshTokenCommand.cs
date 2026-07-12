using Application.Abstractions.Messaging;
using Application.Features.Authentication.Responses;

namespace Application.Features.Authentication.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<AuthResponse>;
