using Domain.Common;
using Domain.Errors;

namespace Domain.Aggregates.UserAggregate.ValueObject
{
    public sealed class Email : ValueObjectBase
    {
        public string Value { get; }

        private Email(string value) => Value = value;

        public static Result<Email> Create(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Result<Email>.Failure(UserErrors.EmailEmpty());

            if (!email.Contains('@') || !email.Contains('.'))
                return Result<Email>.Failure(UserErrors.EmailInvalid());

            if (email.Length > 256)
                return Result<Email>.Failure(UserErrors.EmailTooLong());

            return Result<Email>.Success(new Email(email.ToLowerInvariant()));
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }
    }
}
