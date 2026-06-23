//using Application.Features.Bookings.Command.MakeBooking;
//using FluentValidation;

//namespace EventManagementSystem.Application
//.Features.Bookings.Commands.MakeBooking;

//public sealed class MakeBookingCommandValidator
//    : AbstractValidator<MakeBookingCommand>
//{
//    public MakeBookingCommandValidator()
//    {
//        RuleFor(x => x.EventId)
//            .NotEmpty();

//        RuleFor(x => x.TicketTypeId)
//            .NotEmpty();

//        //RuleFor(x => x.UserId)
//        //    .NotEmpty();

//        RuleFor(x => x.Quantity)
//            .GreaterThan(0);
//    }
//}