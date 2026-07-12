using FluentValidation;

namespace Application.Features.Bookings.Command.ConfirmDeferredPayment;

public sealed class ConfirmDeferredPaymentCommandValidator : AbstractValidator<ConfirmDeferredPaymentCommand>
{
    public ConfirmDeferredPaymentCommandValidator()
    {
        RuleFor(x => x.ReferenceCode)
            .NotEmpty().WithMessage("Reference code is required.")
            .Matches("^FAW-[A-F0-9]{8}$").WithMessage("Invalid reference code format.");
    }
}
