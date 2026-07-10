using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.Events;
using Domain.Common;

namespace Domain.Aggregates.UserAggregate
{
    public class User : AggregateRoot<UserId>
    {
        public string FirstName { get; private set; } = string.Empty;
        public string LastName { get; private set; } = string.Empty;
        public Email Email { get; private set; }
        public string PasswordHash { get; private set; }
        public Role Role { get; private set; }
        public bool IsApproved { get; private set; }
        public string FullName => $"{FirstName} {LastName}".Trim();

        private User() { }

        public static Result<User> Create(
            string firstName,
            string lastName,
            Email email,
            string passwordHash,
            Role role,
            DateTime utcNow,
            bool isApproved = true)
        {
            var user = new User
            {
                Id = UserId.Create(Guid.NewGuid()).Value,
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Email = email,
                PasswordHash = passwordHash,
                Role = role,
                IsApproved = isApproved
            };

            user.RaiseDomainEvent(new UserRegisteredEvent(user.Id, email, utcNow));

            return Result<User>.Success(user);
        }

        public void Approve() => IsApproved = true;

        public string GetPasswordHash() => PasswordHash;
    }
}
