using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;

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
        private readonly ICacheService _cache;

        public ConfirmBookingCommandHandler(IBookingRepository bookingRepository,
                                            IEventRepository eventRepository,
                                            IUnitOfWork unitOfWork,
                                            TimeProvider dateTimeProvider,
                                            IUserRepository userRepository,
                                            ICurrentUserService currentUserService,
                                            ICacheService cache)
        {
            _bookingRepository = bookingRepository;
            _eventRepository = eventRepository;
            _unitOfWork = unitOfWork;
            _dateTimeProvider = dateTimeProvider;
            _userRepository = userRepository;
            _currentUserService = currentUserService;
            _cache = cache;
        }

        public async Task<Result<bool>> Handle(ConfirmBookingCommand request, CancellationToken cancellationToken)
        {
            var bookingIdResult = BookingId.Create(request.BookingId);
            if (bookingIdResult.IsFailure)
            {
                return Result<bool>.Failure(bookingIdResult.Errors.ToArray());
            }

            var booking = await _bookingRepository.GetByIdAsync(bookingIdResult.Value, cancellationToken);

            if (booking is null)
            {
                return Result<bool>.Failure(BookingErrors.BookingNotFound(bookingIdResult.Value));
            }

            var currentUserIdResult = _currentUserService.GetCurrentUserId();
            if (currentUserIdResult.IsFailure)
            {
                return Result<bool>.Failure(currentUserIdResult.Errors.ToArray());
            }

            var userResult = await _userRepository.GetByIdAsync(currentUserIdResult.Value, cancellationToken);
            if (userResult is null)
            {
                return Result<bool>.Failure(UserErrors.NotFound());
            }

            if (userResult.Role != Domain.Aggregates.UserAggregate.ValueObject.Role.Admin
                && userResult.Role != Domain.Aggregates.UserAggregate.ValueObject.Role.Organizer)
            {
                return Result<bool>.Failure(Error.Unauthorized(
                    "Booking.UnauthorizedConfirm",
                    "Only admins and organizers can confirm bookings."));
            }

            var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;

            var confirmResult = booking.Confirm(utcNow);
            if (confirmResult.IsFailure)
            {
                return Result<bool>.Failure(confirmResult.Errors.ToArray());
            }

            var eventResult = await _eventRepository.GetByIdAsync(booking.EventId, cancellationToken);
            if (eventResult is null)
            {
                return Result<bool>.Failure(EventErrors.EventNotFound(booking.EventId));
            }

            var confirmSeatsResult = eventResult.ConfirmReservation(
                booking.TicketTypeId,
                booking.Quantity,
                utcNow);
            if (confirmSeatsResult.IsFailure)
            {
                return Result<bool>.Failure(confirmSeatsResult.Errors.ToArray());
            }

            var rowsAffected = await _unitOfWork.CommitAsync(cancellationToken);

            if (rowsAffected <= 0)
            {
                return Result<bool>.Failure(BookingErrors.BookingConfirmationFailed());
            }

            await _cache.RemoveAsync($"event:details:{booking.EventId.Value}", cancellationToken);

            return Result<bool>.Success(true);
        }
    }
}
