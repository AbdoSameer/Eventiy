
using FluentValidation;

namespace Application.Features.Bookings.Command.CreateBooking
{
    public class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
    {
        public CreateBookingCommandValidator()
        {
            RuleFor(x => x.EventId)
                .NotEmpty().WithMessage("Event ID is required.");

            RuleFor(x => x.TicketTypeId)
                .NotEmpty().WithMessage("Ticket Type ID is required.");
            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than zero.");
        

        }
    }
}
