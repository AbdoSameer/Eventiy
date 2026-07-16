using Domain.Common;
using Domain.Errors;

namespace Domain.Aggregates.UserAggregate.ValueObject
{
    public sealed class Role : ValueObjectBase
    {
        public static readonly Role Attendee = new("Attendee");
        public static readonly Role Organizer = new("Organizer");
        public static readonly Role Admin = new("Admin");

        public string Value { get; }
        private Role(string value) => Value = value;

        public static Result<Role> FromString(string role) =>
            role switch
            {
                "Attendee" => Result<Role>.Success(Attendee),
                "Organizer" => Result<Role>.Success(Organizer),
                "Admin" => Result<Role>.Success(Admin),
                _ => Result<Role>.Failure(UserErrors.RoleInvalid())
            };

        public bool CanCreateEvents => this == Organizer || this == Admin;
        public bool CanApproveOrganizers => this == Admin;
        public bool RequiresApproval => this == Organizer;

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value;
        }
    }
}
