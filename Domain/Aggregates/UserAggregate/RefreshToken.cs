using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Primitives;

namespace Domain.Aggregates.UserAggregate;

public sealed class RefreshToken : Entity<RefreshTokenId>
{
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresOnUtc { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? RevokedOnUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    private RefreshToken() : base(default!) { }

    private RefreshToken(RefreshTokenId id) : base(id) { }

    public static RefreshToken Create(string tokenHash, DateTime expiresOnUtc, DateTime utcNow) => new()
    {
        Id = RefreshTokenId.FromDatabase(0),
        TokenHash = tokenHash,
        ExpiresOnUtc = expiresOnUtc,
        CreatedOnUtc = utcNow
    };

    public Result Revoke(DateTime utcNow, string? replacedByTokenHash = null)
    {
        if (RevokedOnUtc is not null)
            return Result.Failure(UserErrors.RefreshTokenAlreadyRevoked());

        RevokedOnUtc = utcNow;
        ReplacedByTokenHash = replacedByTokenHash;
        return Result.Success();
    }

    public bool IsActiveAt(DateTime utcNow) => RevokedOnUtc is null && utcNow < ExpiresOnUtc;

    public bool IsExpiredAt(DateTime utcNow) => RevokedOnUtc is null && utcNow >= ExpiresOnUtc;

    public bool IsRevoked => RevokedOnUtc is not null;
}
