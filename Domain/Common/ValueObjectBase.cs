namespace Domain.Common
{
    public abstract class ValueObjectBase
    {
        protected abstract IEnumerable<object> GetEqualityComponents();

        public override bool Equals(object? obj)
        {
            if (obj == null || obj.GetType() != GetType())
                return false;

            var other = (ValueObjectBase)obj;

            return GetEqualityComponents()
                .SequenceEqual(other.GetEqualityComponents());
        }

        public override int GetHashCode()
        {
            return GetEqualityComponents()
                .Select(x => x?.GetHashCode() ?? 0)
                .Aggregate((x, y) => x ^ y);
        }

        public static bool operator ==(ValueObjectBase left, ValueObjectBase right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ValueObjectBase left, ValueObjectBase right)
        {
            return !Equals(left, right);
        }
    }
}