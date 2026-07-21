using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.UserAggregate.Events
{
    public class AllRefreshTokensRevokedEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(AllRefreshTokensRevokedEvent);
        public override string Domain => "User";
        public UserId UserId { get; }
        public int RevokedCount { get; }

        public AllRefreshTokensRevokedEvent(
            UserId userId,
            int revokedCount,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            UserId = userId;
            RevokedCount = revokedCount;
        }
    }
}
