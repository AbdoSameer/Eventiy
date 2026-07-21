using Application.Abstractions.Messaging;

namespace Application.Features.Authentication.Commands.RefreshToken;

public sealed record RevokeRefreshTokenCommand(string RefreshToken) : ICommand<bool>;