using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Common;

namespace Application.Features.Bookings.Command.CancelBooking
{
    public class CancelBookingCommandHandler : ICommandHandler<CancelBookingCommand, bool>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CancelBookingCommandHandler(IBookingRepository bookingRepository,
                                           IUnitOfWork unitOfWork)
        {
            _bookingRepository = bookingRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<bool>> Handle(CancelBookingCommand request, CancellationToken cancellationToken)
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

            var CancelResult = booking.Cancel();
            if (CancelResult.IsFailure)
            {
                return Result<bool>.Failure(CancelResult.Error);
            }

            var result = await _unitOfWork.CommitAsync(cancellationToken);

            if (result <= 0)
            {
                return Result<bool>.Failure("Failed to cancel booking");
            }

            return Result<bool>.Success(true);

        }
    }
}
