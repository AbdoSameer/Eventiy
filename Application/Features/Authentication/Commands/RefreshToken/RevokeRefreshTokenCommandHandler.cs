using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Abstractions.Persistence;
using Domain.Common;
using Domain.Errors;

namespace Application.Features.Authentication.Commands.RefreshToken;

internal sealed class RevokeRefreshTokenCommandHandler(
    IUserRepository userRepository,
    IJwtTokenGenerator jwtGenerator,
    IUnitOfWork unitOfWork,
    TimeProvider dateTimeProvider) : ICommandHandler<RevokeRefreshTokenCommand, bool>
{
    public async Task<Result<bool>> Handle(
        RevokeRefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var tokenHash = jwtGenerator.HashToken(command.RefreshToken);
        var user = await userRepository.GetByRefreshTokenHashAsync(tokenHash, cancellationToken);

        if (user is null)
            return Result<bool>.Failure(UserErrors.RefreshTokenNotFoundOrInactive());

        var existingToken = user.RefreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash);
        if (existingToken is null || !existingToken.IsActiveAt(dateTimeProvider.GetUtcNow().UtcDateTime))
            return Result<bool>.Failure(UserErrors.RefreshTokenNotFoundOrInactive());

        var utcNow = dateTimeProvider.GetUtcNow().UtcDateTime;
        var revokeResult = existingToken.Revoke(utcNow, null);
        if (revokeResult.IsFailure)
            return Result<bool>.Failure(revokeResult.Errors.ToArray());

        await unitOfWork.CommitAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}