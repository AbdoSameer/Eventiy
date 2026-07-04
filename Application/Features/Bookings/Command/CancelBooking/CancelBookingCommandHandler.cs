using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
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
        private readonly IUserRepository _userRepository;
        private readonly ICurrentUserService _currentUserService;
       

        public CancelBookingCommandHandler(IBookingRepository bookingRepository,
                                           IUnitOfWork unitOfWork,
                                           IDateTimeProvider dateTimeProvider,
                                           IUserRepository userRepository,
                                           ICurrentUserService currentUserService)
        {
            _bookingRepository = bookingRepository;
            _unitOfWork = unitOfWork;
            _dateTimeProvider = dateTimeProvider;
            _currentUserService = currentUserService;
            _userRepository = userRepository;

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

            var CurrentUserIdResult = _currentUserService.GetCurrentUserId();
            if (CurrentUserIdResult.IsFailure)
            {
                return Result<bool>.Failure(CurrentUserIdResult.Errors.ToArray());
            }

            var UserResult = await _userRepository.GetByIdAsync(CurrentUserIdResult.Value);
            if (UserResult is null)
            {
                return Result<bool>.Failure(UserErrors.NotFound());
            }

            var metadata = new EventMetadata(Guid.NewGuid().ToString(), null, null);

            var CancelResult = booking.Cancel(UserResult.Role,_dateTimeProvider, metadata);
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
