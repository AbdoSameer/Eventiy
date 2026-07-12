namespace Domain.Aggregates.UserAggregate;

public sealed class RefreshToken
{
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresOnUtc { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? RevokedOnUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsActive => RevokedOnUtc is null && DateTime.UtcNow < ExpiresOnUtc;

    private RefreshToken() { }

    public static RefreshToken Create(string tokenHash, DateTime expiresOnUtc, DateTime utcNow) => new()
    {
        TokenHash = tokenHash,
        ExpiresOnUtc = expiresOnUtc,
        CreatedOnUtc = utcNow
    };

    public void Revoke(DateTime utcNow, string? replacedByTokenHash = null)
    {
        RevokedOnUtc = utcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
