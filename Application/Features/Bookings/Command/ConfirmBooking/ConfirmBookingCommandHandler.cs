using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Persistence.Repositories;

namespace Application.Features.Bookings.Command.ConfirmBooking
{
    public class ConfirmBookingCommandHandler 
        : ICommandHandler<ConfirmBookingCommand, bool>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDateTimeProvider _dateTimeProvider;

        public ConfirmBookingCommandHandler(IBookingRepository bookingRepository,
                                            IUnitOfWork unitOfWork,
                                            IDateTimeProvider dateTimeProvider)
        {
            _bookingRepository = bookingRepository;
            _unitOfWork = unitOfWork;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<Result<bool>> Handle(ConfirmBookingCommand request, CancellationToken cancellationToken)
        {
            var bookingIdResult = BookingId.Create(request.BookingId);
            if (bookingIdResult.IsFailure)
            {
                return Result<bool>.Failure(bookingIdResult.Errors.ToArray());
            }

            var booking = await _bookingRepository.GetByIdAsync(bookingIdResult.Value);

            if (booking == null)
            {
                return Result<bool>.Failure(BookingErrors.BookingNotFound(bookingIdResult.Value));
            }

            var metadata = new EventMetadata(Guid.NewGuid().ToString(), null, null);

            var ConfirmResult = booking.Confirm(_dateTimeProvider, metadata);
            if (ConfirmResult.IsFailure)
            {
                return Result<bool>.Failure(ConfirmResult.Errors.ToArray());
            }

            var rowsAffected = await _unitOfWork.CommitAsync(cancellationToken);

            if (rowsAffected <= 0)
            {
                return Result<bool>.Failure(BookingErrors.BookingCreationFailed());
            }

            return Result<bool>.Success(true);



        }
    }
}

