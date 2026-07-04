using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;

namespace Domain.Aggregates.UserAggregate
{
    public class User : AggregateRoot<UserId>
    {
        public UserId Id { get; private set; }
        public Email Email { get; private set; }
        public string PasswordHash { get; private set; } 
        public Role Role { get; private set; }
        public bool IsApproved { get; private set; }

        private User() { }

        public static Result<User> Create(
            Email email,
            string passwordHash,
            Role role,
            IDateTimeProvider dt,
            EventMetadata eventMetadata,
            bool isApproved = true)
        {
            var user = new User
            {
                Id = UserId.Create(Guid.NewGuid()).Value,
                Email = email,
                PasswordHash = passwordHash,
                Role = role,
                IsApproved = isApproved
            };

            user.RaiseDomainEvent(DomainEventFactory
                .CreateUserRegistered(user.Id, email, dt.UtcNow, eventMetadata));
            
            return Result<User>.Success(user);
        }

        public void Approve() => IsApproved = true;

        public string GetPasswordHash() => PasswordHash;
    }
}
