using FluentValidation;


namespace Application.Features.Authentication.Commands.Register
{
    public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
    {
        public RegisterUserCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Email format is invalid.")
                .MaximumLength(256);

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
                .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
                .Matches("[0-9]").WithMessage("Password must contain at least one digit.");

            RuleFor(x => x.FirstName)
                .NotEmpty().MaximumLength(100);

            RuleFor(x => x.LastName)
                .NotEmpty().MaximumLength(100);

            RuleFor(x => x.Role)
                .Must(r => r is "Attendee" or "Organizer")
                .WithMessage("Role must be 'Attendee' or 'Organizer'.");
        }
    }

}
