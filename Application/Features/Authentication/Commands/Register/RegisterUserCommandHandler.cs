using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Application.Features.Authentication.Responses;
using Domain.Aggregates.UserAggregate;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Persistence.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Authentication.Commands.Register
{
    internal sealed class RegisterUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtGenerator,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider) : ICommandHandler<RegisterUserCommand, AuthResponse>
    {
        public async Task<Result<AuthResponse>> Handle(
            RegisterUserCommand command, CancellationToken cancellationToken)
        {
            var emailResult = Email.Create(command.Email);
            if (emailResult.IsFailure)
                return Result<AuthResponse>.Failure(emailResult.Errors.ToArray());

            var existingUser = await userRepository.GetByEmailAsync(emailResult.Value, cancellationToken);
            if (existingUser is not null)
                return Result<AuthResponse>.Failure(UserErrors.EmailAlreadyExists());

            var roleResult = Role.FromString(command.Role);
            if (roleResult.IsFailure)
                return Result<AuthResponse>.Failure(roleResult.Errors.ToArray());

            var passwordHash = passwordHasher.Hash(command.Password);

            var metadata = EventMetadata.Create(Guid.NewGuid().ToString(), null, null);
        
            var userResult = User.Create(
                emailResult.Value, passwordHash, roleResult.Value,
                dateTimeProvider, metadata);

            if (userResult.IsFailure)
                return Result<AuthResponse>.Failure(userResult.Errors.ToArray());

            var user = userResult.Value;

            await userRepository.AddAsync(user, cancellationToken);

            var rows = await unitOfWork.CommitAsync(cancellationToken);
            if (rows <= 0)
                return Result<AuthResponse>.Failure(
                    Error.Failure("User.RegistrationFailed", "Failed to persist user."));

            var (token, expiresAt) = jwtGenerator.GenerateToken(user);

            return Result<AuthResponse>.Success(new AuthResponse(
                user.Id.Value,
                user.Email.Value,
                user.Role.Value,
                token,
                expiresAt));
        }
    }

}
