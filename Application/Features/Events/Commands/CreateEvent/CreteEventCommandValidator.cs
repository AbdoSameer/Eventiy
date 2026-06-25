using FluentValidation;

namespace Application.Features.Events.Commands.CreateEvent
{
    internal class CreteEventCommandValidator : AbstractValidator<CreateEventCommand>
    {
        public CreteEventCommandValidator()
        { 

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Event name is required.")
                .MaximumLength(100).WithMessage("Event name must not exceed 100 characters.");
        
            RuleFor(x => x.Date)
                .GreaterThan(DateTime.Now).WithMessage("Event date must be in the future.");
            
            RuleFor(x => x.Capacity)
                .GreaterThan(0).WithMessage("Event capacity must be greater than zero.");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Event description must not exceed 500 characters.");

            RuleFor(x => x.Location)
                .NotNull().WithMessage("Event location is required.")
                .SetValidator(new AddressResponseValidator());
        }

    }
}
