using Application.Abstractions.Security;
using Domain.Common;
using MediatR;

namespace Application.Abstractions.Behaviors;

public sealed class AuthorizationPipelineBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IAuthorizableRequest
    where TResponse : IValidationResult<TResponse>
{
    private readonly ICurrentUserService _currentUser;

    public AuthorizationPipelineBehavior(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var role = _currentUser.GetCurrentUserRole();

        if (role is null || !request.RequiredRoles.Contains(role))
        {
            return TResponse.CreateFailure([
                Error.Unauthorized(
                    $"Authorization.{typeof(TRequest).Name}",
                    $"This action requires one of the following roles: {string.Join(", ", request.RequiredRoles)}.")
            ]);
        }

        return await next();
    }
}
