using Domain.Common;

namespace Domain.Aggregates.UserAggregate.ValueObject
{
    public class UserId : ValueObjectBase
    {
        public Guid Value { get; }
        protected UserId() { }

        private UserId(Guid value)
        {
            Value = value;
        }

        public static UserId FromDatabase(Guid value) => new UserId(value);
       
        public static Result<UserId> Create(Guid value)
        {
            if (value == Guid.Empty)
                return Result<UserId>.Failure(Error.Validation("User.InvalidId", "The provided User ID Is invalid"));

            return Result<UserId>.Success(new UserId(value));
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }
    }


}
