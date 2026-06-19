using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Common;

namespace Application.Features.Bookings.Command.ConfirmBooking
{
    public class ConfirmBookingCommandHandler 
        : ICommandHandler<ConfirmBookingCommand, bool>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ConfirmBookingCommandHandler(IBookingRepository bookingRepository,
                                            IUnitOfWork unitOfWork)
        {
            _bookingRepository = bookingRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<bool>> Handle(ConfirmBookingCommand request, CancellationToken cancellationToken)
        {
            var bookingIdResult = BookingId.Create(request.BookingId);
            if (bookingIdResult.IsFailure)
            {
                return Result<bool>.Failure(bookingIdResult.Error);
            }

            var booking = await _bookingRepository.GetByIdAsync(bookingIdResult.Value);

            if (booking == null)
            {
                return Result<bool>.Failure("Booking not found");
            }

            var ConfirmResult = booking.Confirm();
            if (ConfirmResult.IsFailure)
            {
                return Result<bool>.Failure(ConfirmResult.Error);
            }

            var result = await _unitOfWork.CommitAsync(cancellationToken);

            if (result <= 0)
            {
                return Result<bool>.Failure("Failed to Confirm booking");
            }

            return Result<bool>.Success(true);



        }
    }
}

