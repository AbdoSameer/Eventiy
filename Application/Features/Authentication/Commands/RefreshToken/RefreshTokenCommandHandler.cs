using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Application.Features.Authentication.Responses;
using Domain.Abstractions.Persistence;
using Domain.Common;
using Domain.Errors;

namespace Application.Features.Authentication.Commands.RefreshToken;

internal sealed class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    IJwtTokenGenerator jwtGenerator,
    IUnitOfWork unitOfWork,
    TimeProvider dateTimeProvider) : ICommandHandler<RefreshTokenCommand, AuthResponse>
{
    public async Task<Result<AuthResponse>> Handle(
        RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var tokenHash = jwtGenerator.HashToken(command.RefreshToken);
        var user = await userRepository.GetByRefreshTokenHashAsync(tokenHash, cancellationToken);

        if (user is null)
            return Result<AuthResponse>.Failure(UserErrors.RefreshTokenNotFoundOrInactive());

        var existingToken = user.RefreshTokens.First(t => t.TokenHash == tokenHash);
        var utcNow = dateTimeProvider.GetUtcNow().UtcDateTime;

        if (existingToken.IsRevoked)
        {
            user.RevokeAllRefreshTokens(utcNow);
            await unitOfWork.CommitAsync(cancellationToken);
            return Result<AuthResponse>.Failure(UserErrors.RefreshTokenReused());
        }

        if (existingToken.IsExpiredAt(utcNow))
            return Result<AuthResponse>.Failure(UserErrors.RefreshTokenExpired());

        var newRefreshTokenRaw = jwtGenerator.GenerateRefreshToken();
        var newRefreshTokenHash = jwtGenerator.HashToken(newRefreshTokenRaw);

        var revokeResult = existingToken.Revoke(utcNow, newRefreshTokenHash);
        if (revokeResult.IsFailure)
            return Result<AuthResponse>.Failure(revokeResult.Errors.ToArray());

        user.IssueRefreshToken(newRefreshTokenHash, utcNow.AddDays(7), utcNow);

        var (accessToken, expiresAt) = jwtGenerator.GenerateToken(user);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<AuthResponse>.Success(new AuthResponse(
            user.Id.Value, user.Email.Value, user.Role.Value, accessToken, expiresAt,
            RefreshToken: newRefreshTokenRaw));
    }
}
