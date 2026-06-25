using Application.Features.Events.Queries.GetEventDetails;
using FluentValidation;
using FluentValidation.Validators;

namespace Application.Features.Events.Commands.CreateEvent
{
    internal class AddressResponseValidator : IPropertyValidator<CreateEventCommand, AddressResponse>
    {
        public string Name => "AddressResponseValidator";

        public string GetDefaultMessageTemplate(string errorCode)
        {
            return "Invalid address.";
        }

        public bool IsValid(ValidationContext<CreateEventCommand> context, AddressResponse value)
        {
            if (value == null)
            {
                context.AddFailure("Address is required.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(value.Street))
            {
                context.AddFailure("Street is required.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(value.City))
            {
                context.AddFailure("City is required.");
                return false;
            }
            return true;
        }
    }
}