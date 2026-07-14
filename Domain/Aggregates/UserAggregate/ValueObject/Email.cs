using System.Text.RegularExpressions;
using Domain.Common;
using Domain.Errors;

namespace Domain.Aggregates.UserAggregate.ValueObject
{
    public sealed class Email : ValueObjectBase
    {
        public string Value { get; }

        private static readonly Regex EmailRegex = new(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private Email(string value) => Value = value;

        public static Result<Email> Create(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Result<Email>.Failure(UserErrors.EmailEmpty());

            if (email.Length > 256)
                return Result<Email>.Failure(UserErrors.EmailTooLong());

            if (!EmailRegex.IsMatch(email))
                return Result<Email>.Failure(UserErrors.EmailInvalid());

            return Result<Email>.Success(new Email(email.ToLowerInvariant()));
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }
    }
}
