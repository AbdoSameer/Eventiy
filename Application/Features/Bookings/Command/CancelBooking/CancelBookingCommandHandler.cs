using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Persistence.Repositories;

namespace Application.Features.Bookings.Command.CancelBooking
{
    public class CancelBookingCommandHandler : ICommandHandler<CancelBookingCommand, bool>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDateTimeProvider _dateTimeProvider;

        public CancelBookingCommandHandler(IBookingRepository bookingRepository,
                                           IUnitOfWork unitOfWork,
                                           IDateTimeProvider dateTimeProvider)
        {
            _bookingRepository = bookingRepository;
            _unitOfWork = unitOfWork;
            _dateTimeProvider = dateTimeProvider;
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

            var metadata = new EventMetadata(Guid.NewGuid().ToString(), null, null);

            var CancelResult = booking.Cancel(_dateTimeProvider, metadata);
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
