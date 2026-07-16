using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.Events;
using Domain.Common;
using Domain.Errors;

namespace Domain.Aggregates.UserAggregate
{
    public class User : AggregateRoot<UserId>
    {
        private readonly List<RefreshToken> _refreshTokens = new();

        public string FirstName { get; private set; } = string.Empty;
        public string LastName { get; private set; } = string.Empty;
        public Email Email { get; private set; }
        public string PasswordHash { get; private set; }
        public Role Role { get; private set; }
        public bool IsApproved { get; private set; }
        public byte[]? RowVersion { get; private set; } = Array.Empty<byte>();
        public string FullName => $"{FirstName} {LastName}".Trim();
        public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

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
            var userIdResult = UserId.Create(Guid.NewGuid());
            if (userIdResult.IsFailure)
                return Result<User>.Failure(userIdResult.Errors.ToArray());

            var user = new User
            {
                Id = userIdResult.Value,
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

        public RefreshToken IssueRefreshToken(string tokenHash, DateTime expiresOnUtc, DateTime utcNow)
        {
            var token = RefreshToken.Create(tokenHash, expiresOnUtc, utcNow);
            _refreshTokens.Add(token);
            return token;
        }

        public Result RevokeRefreshToken(string tokenHash, DateTime utcNow)
        {
            var token = _refreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash && t.IsActiveAt(utcNow));
            if (token is null)
                return Result.Failure(UserErrors.RefreshTokenNotFoundOrInactive());

            token.Revoke(utcNow);
            return Result.Success();
        }

        public void RevokeAllRefreshTokens(DateTime utcNow)
        {
            foreach (var token in _refreshTokens.Where(t => t.IsActiveAt(utcNow)))
            {
                token.Revoke(utcNow);
            }
        }
    }
}
