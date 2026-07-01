using Application.Abstractions.Security;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Infrastructure.Authentication
{
    public sealed class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _accessor;

        public CurrentUserService(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public bool IsAuthenticated 
            => _accessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
        public Result<UserId> GetCurrentUserId()
        {
            var userIdClaim = _accessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            return Guid.TryParse(userIdClaim, out var userId)
                ? Result<UserId>.Success(UserId.FromDatabase(userId))
                : Result<UserId>.Failure(UserErrors.UserNotAuthenticated());
        }

    }
}
