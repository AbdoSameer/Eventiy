using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.UserAggregate.Events
{
    public class RefreshTokenIssuedEvent : DomainEvent, IDomainEvent
    {
        public override string Name => nameof(RefreshTokenIssuedEvent);
        public override string Domain => "User";
        public UserId UserId { get; }
        public string TokenHash { get; }
        public DateTime ExpiresOnUtc { get; }

        public RefreshTokenIssuedEvent(
            UserId userId,
            string tokenHash,
            DateTime expiresOnUtc,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            UserId = userId;
            TokenHash = tokenHash;
            ExpiresOnUtc = expiresOnUtc;
        }
    }
}
