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
        private readonly IEventRepository _eventRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _dateTimeProvider;
        private readonly IUserRepository _userRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IEventMetadataFactory _metadataFactory;

        public ConfirmBookingCommandHandler(IBookingRepository bookingRepository,
                                            IEventRepository eventRepository,
                                            IUnitOfWork unitOfWork,
                                            TimeProvider dateTimeProvider,
                                            IUserRepository userRepository,
                                            ICurrentUserService currentUserService,
                                            IEventMetadataFactory metadataFactory)
        {
            _bookingRepository = bookingRepository;
            _eventRepository = eventRepository;
            _unitOfWork = unitOfWork;
            _dateTimeProvider = dateTimeProvider;
            _userRepository = userRepository;
            _currentUserService = currentUserService;
            _metadataFactory = metadataFactory;
        }

        public async Task<Result<bool>> Handle(ConfirmBookingCommand request, CancellationToken cancellationToken)
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

            if (UserResult.Role != Domain.Aggregates.UserAggregate.ValueObject.Role.Admin
                && UserResult.Role != Domain.Aggregates.UserAggregate.ValueObject.Role.Organizer)
            {
                return Result<bool>.Failure(Error.Unauthorized(
                    "Booking.UnauthorizedConfirm",
                    "Only admins and organizers can confirm bookings."));
            }

            var metadata = _metadataFactory.Create();
            var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;

            var ConfirmResult = booking.Confirm(utcNow, metadata);
            if (ConfirmResult.IsFailure)
            {
                return Result<bool>.Failure(ConfirmResult.Errors.ToArray());
            }

            var eventResult = await _eventRepository.GetByIdAsync(booking.EventId, cancellationToken);
            if (eventResult is null)
            {
                return Result<bool>.Failure(EventErrors.EventNotFound(booking.EventId));
            }

            var confirmSeatsResult = eventResult.ConfirmReservation(
                booking.TicketTypeId,
                booking.Quantity,
                utcNow,
                metadata);
            if (confirmSeatsResult.IsFailure)
            {
                return Result<bool>.Failure(confirmSeatsResult.Errors.ToArray());
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
