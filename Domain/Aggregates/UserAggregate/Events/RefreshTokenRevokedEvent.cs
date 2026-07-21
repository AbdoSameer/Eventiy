using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.UserAggregate.Events
{
    public class RefreshTokenRevokedEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(RefreshTokenRevokedEvent);
        public override string Domain => "User";
        public UserId UserId { get; }
        public string TokenHash { get; }
        public string? ReplacedByTokenHash { get; }

        public RefreshTokenRevokedEvent(
            UserId userId,
            string tokenHash,
            DateTime occurredOnUtc,
            string? replacedByTokenHash = null) : base(occurredOnUtc)
        {
            UserId = userId;
            TokenHash = tokenHash;
            ReplacedByTokenHash = replacedByTokenHash;
        }
    }
}
