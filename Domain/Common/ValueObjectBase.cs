// Domain/Common/ValueObjectBase.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Domain.Common
{
    public abstract class ValueObjectBase : IEquatable<ValueObjectBase>
    {
        protected abstract IEnumerable<object> GetEqualityComponents();

        public override bool Equals(object? obj)
        {
            if (obj is null || obj.GetType() != GetType())
                return false;

            return GetEqualityComponents()
                .SequenceEqual(((ValueObjectBase)obj).GetEqualityComponents());
        }

        public override int GetHashCode()
        {
            return GetEqualityComponents()
                .Aggregate(default(int), HashCode.Combine);
        }

        public bool Equals(ValueObjectBase? other)
        {
            if (other is null)
                return false;

            return GetEqualityComponents()
                .SequenceEqual(other.GetEqualityComponents());
        }

        public static bool operator ==(ValueObjectBase? left, ValueObjectBase? right)
        {
            if (left is null && right is null) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(ValueObjectBase? left, ValueObjectBase? right)
            => !(left == right);
    }
}