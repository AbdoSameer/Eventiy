using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
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
        private readonly IUserRepository _userRepository;
        private readonly ICurrentUserService _currentUserService;

        public ConfirmBookingCommandHandler(IBookingRepository bookingRepository,
                                            IUnitOfWork unitOfWork,
                                            IDateTimeProvider dateTimeProvider,
                                            IUserRepository userRepository,
                                            ICurrentUserService currentUserService)
        {
            _bookingRepository = bookingRepository;
            _unitOfWork = unitOfWork;
            _dateTimeProvider = dateTimeProvider;
            _userRepository = userRepository;
            _currentUserService = currentUserService;
        }

        public async Task<Result<bool>> Handle(ConfirmBookingCommand request, CancellationToken cancellationToken)
        {
            var bookingIdResult = BookingId.Create(request.BookingId);
            if (bookingIdResult.IsFailure)
            {
                return Result<bool>.Failure(bookingIdResult.Errors.ToArray());
            }

            var BookingResult = await _bookingRepository.GetByIdAsync(bookingIdResult.Value);

            if (BookingResult is null)
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

            var ConfirmResult = BookingResult.Confirm(UserResult.Role,_dateTimeProvider, metadata);
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

