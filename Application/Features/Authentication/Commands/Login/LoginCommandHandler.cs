using Application.Abstractions.Messaging;
using Application.Abstractions.Security;
using Application.Features.Authentication.Responses;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;


namespace Application.Features.Authentication.Commands.Login
{
    internal sealed class LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtGenerator) : ICommandHandler<LoginCommand, AuthResponse>
    {
        public async Task<Result<AuthResponse>> Handle(
            LoginCommand command, CancellationToken cancellationToken)
        {
            var emailResult = Email.Create(command.Email);
            if (emailResult.IsFailure)
                return Result<AuthResponse>.Failure(UserErrors.InvalidCredentials()); // غموض مقصود

            var user = await userRepository.GetByEmailAsync(emailResult.Value, cancellationToken);
            if (user is null)
                return Result<AuthResponse>.Failure(UserErrors.InvalidCredentials());

            var isPasswordValid = passwordHasher.Verify(command.Password, user.GetPasswordHash());
            if (!isPasswordValid)
                return Result<AuthResponse>.Failure(UserErrors.InvalidCredentials());

            if (user.Role == Role.Organizer && !user.IsApproved)
                return Result<AuthResponse>.Success(new AuthResponse(
                    user.Id.Value, user.Email.Value, user.Role.Value, null, null, true));

            var (token, expiresAt) = jwtGenerator.GenerateToken(user);

            return Result<AuthResponse>.Success(new AuthResponse(
                user.Id.Value, user.Email.Value, user.Role.Value, token, expiresAt));
        }
    }

}
