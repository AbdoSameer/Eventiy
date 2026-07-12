using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Common;
using Domain.Errors;
using Domain.Abstractions.Persistence;

namespace Application.Features.Bookings.Command.CancelBooking
{
    public class CancelBookingCommandHandler : ICommandHandler<CancelBookingCommand, bool>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _dateTimeProvider;
        private readonly IUserRepository _userRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ICacheService _cache;

        public CancelBookingCommandHandler(IBookingRepository bookingRepository,
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

        public async Task<Result<bool>> Handle(CancelBookingCommand request, CancellationToken cancellationToken)
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

            var CurrentUserIdResult = _currentUserService.GetCurrentUserId();
            if (CurrentUserIdResult.IsFailure)
            {
                return Result<bool>.Failure(CurrentUserIdResult.Errors.ToArray());
            }

            var UserResult = await _userRepository.GetByIdAsync(CurrentUserIdResult.Value, cancellationToken);
            if (UserResult is null)
            {
                return Result<bool>.Failure(UserErrors.NotFound());
            }

            var isAdminOrOrg = UserResult.Role == Domain.Aggregates.UserAggregate.ValueObject.Role.Admin
                            || UserResult.Role == Domain.Aggregates.UserAggregate.ValueObject.Role.Organizer;

            var isOwnBooking = booking.UserId == CurrentUserIdResult.Value;

            if (!isAdminOrOrg && !isOwnBooking)
            {
                return Result<bool>.Failure(Error.Unauthorized(
                    "Booking.UnauthorizedCancel",
                    "You can only cancel your own bookings."));
            }

            if (!isAdminOrOrg && isOwnBooking && booking.Status != BookingStatusEnum.Pending)
            {
                return Result<bool>.Failure(BookingErrors.CannotCancelBooking(
                    bookingIdResult.Value, booking.Status));
            }

            var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;
            var wasConfirmed = booking.Status == BookingStatusEnum.Confirmed;

            var CancelResult = booking.Cancel(utcNow);
            if (CancelResult.IsFailure)
            {
                return Result<bool>.Failure(CancelResult.Errors.ToArray());
            }

            var eventResult = await _eventRepository.GetByIdAsync(booking.EventId, cancellationToken);
            if (eventResult is null)
            {
                return Result<bool>.Failure(EventErrors.EventNotFound(booking.EventId));
            }

            Result seatsResult;
            if (wasConfirmed)
            {
                seatsResult = eventResult.RefundSeats(
                    booking.TicketTypeId,
                    booking.Quantity,
                    utcNow);
            }
            else
            {
                seatsResult = eventResult.ReleaseSeats(
                    booking.TicketTypeId,
                    booking.Quantity,
                    utcNow);
            }

            if (seatsResult.IsFailure)
            {
                return Result<bool>.Failure(seatsResult.Errors.ToArray());
            }

            var result = await _unitOfWork.CommitAsync(cancellationToken);

            if (result <= 0)
            {
                return Result<bool>.Failure(BookingErrors.CannotCancelBooking(bookingIdResult.Value, booking.Status));
            }

            await _cache.RemoveAsync($"event:details:{booking.EventId.Value}", cancellationToken);

            return Result<bool>.Success(true);

        }
    }
}
