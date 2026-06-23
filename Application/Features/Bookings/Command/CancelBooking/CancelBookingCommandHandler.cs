using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
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
                return Result<bool>.Failure(bookingIdResult.Errors.ToArray());
            }

            var booking = await _bookingRepository.GetByIdAsync(bookingIdResult.Value);

            if (booking is null)
            {
                return Result<bool>.Failure(BookingErrors.BookingNotFound(bookingIdResult.Value));

            }

            var CancelResult = booking.Cancel();
            if (CancelResult.IsFailure)
            {
                return Result<bool>.Failure(CancelResult.Errors.ToArray());
            }

            var result = await _unitOfWork.CommitAsync(cancellationToken);

            if (result <= 0)
            {
                return Result<bool>.Failure(BookingErrors.CannotCancelBooking(bookingIdResult.Value,booking.Status));
            }

            return Result<bool>.Success(true);

        }
    }
}
