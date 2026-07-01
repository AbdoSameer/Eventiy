using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;

namespace Application.Abstractions.Security
{
    public interface ICurrentUserService
    {
        Result<UserId> GetCurrentUserId();
        bool IsAuthenticated { get; }
    }
}
