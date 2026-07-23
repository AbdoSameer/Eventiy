using Domain.Common;
using Domain.Errors;

namespace Domain.Aggregates.BookingAggregate.ValueObject
{
    public class BookingId : ValueObjectBase
    {
    public Guid Value { get; }

        protected BookingId() { }

        private BookingId(Guid value)
        {
            Value = value;
        }

        public static BookingId FromDatabase(Guid value) => new BookingId(value);

        public static Result<BookingId> Create(Guid value)
        {
            if (value == Guid.Empty)
                return Result<BookingId>.Failure(BookingErrors.InvalidBookingId(value));

            return Result<BookingId>.Success(new BookingId(value));
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }

        public override string ToString() => Value.ToString();
    }
}