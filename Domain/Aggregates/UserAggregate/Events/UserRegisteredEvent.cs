using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.UserAggregate.Events
{
    public class UserRegisteredEvent : DomainEvent
    {
        public override string Name => nameof(UserRegisteredEvent);
        public override string Domain => "User";
        public UserId UserId { get; }
        public Email Email { get; }
        
        public UserRegisteredEvent(
            UserId userId,
            Email email,
            DateTime occurredOnUtc) : base(occurredOnUtc)
        {
            UserId = userId;
            Email = email;

        }
    }
}
