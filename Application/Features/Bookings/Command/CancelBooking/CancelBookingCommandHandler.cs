using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Abstractions.Persistence;
using static Application.Abstractions.Caching.CacheKeys;

namespace Application.Features.Bookings.Command.CancelBooking
{
    public class CancelBookingCommandHandler : ICommandHandler<CancelBookingCommand, bool>
    {
        private readonly TimeProvider _dateTimeProvider;
        private readonly ICurrentUserService _currentUserService;
        private readonly ICacheService _cache;
        private readonly IBookingRepository _bookingRepo;
        private readonly IEventRepository _eventRepo;
        private readonly IUserRepository _userRepo;
        private readonly IUnitOfWork _uow;

        public CancelBookingCommandHandler(
            TimeProvider dateTimeProvider,
            ICurrentUserService currentUserService,
            ICacheService cache,
            IBookingRepository bookingRepo,
            IEventRepository eventRepo,
            IUserRepository userRepo,
            IUnitOfWork uow)
        {
            _dateTimeProvider = dateTimeProvider;
            _currentUserService = currentUserService;
            _cache = cache;
            _bookingRepo = bookingRepo;
            _eventRepo = eventRepo;
            _userRepo = userRepo;
            _uow = uow;
        }

        public async Task<Result<bool>> Handle(CancelBookingCommand request, CancellationToken cancellationToken)
        {
            var bookingIdResult = BookingId.Create(request.BookingId);
            if (bookingIdResult.IsFailure)
                return Result<bool>.Failure(bookingIdResult.Errors.ToArray());

            var currentUserIdResult = _currentUserService.GetCurrentUserId();
            if (currentUserIdResult.IsFailure)
                return Result<bool>.Failure(currentUserIdResult.Errors.ToArray());

            var bookingId = bookingIdResult.Value;
            var currentUserId = currentUserIdResult.Value;

            var booking = await _bookingRepo.GetByIdAsync(bookingId, cancellationToken);
            if (booking is null)
                return Result<bool>.Failure(BookingErrors.BookingNotFound(bookingId.Value));

            var userResult = await _userRepo.GetByIdAsync(currentUserId, cancellationToken);
            if (userResult is null)
                return Result<bool>.Failure(UserErrors.NotFound());

            var isAdminOrOrg = userResult.Role == Role.Admin
                            || userResult.Role == Role.Organizer;

            var isOwnBooking = booking.UserId == currentUserId;

            if (!isAdminOrOrg && !isOwnBooking)
                return Result<bool>.Failure(Error.Unauthorized(
                    "Booking.UnauthorizedCancel",
                    "You can only cancel your own bookings."));

            if (!isAdminOrOrg && isOwnBooking && booking.Status != BookingStatusEnum.Pending)
                return Result<bool>.Failure(BookingErrors.CannotCancelBooking(bookingId.Value, booking.Status));

            var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;
            var wasConfirmed = booking.Status == BookingStatusEnum.Confirmed;

            var cancelResult = booking.Cancel(utcNow);
            if (cancelResult.IsFailure)
                return Result<bool>.Failure(cancelResult.Errors.ToArray());

            var eventResult = await _eventRepo.GetByIdAsync(booking.EventId, cancellationToken);
            if (eventResult is null)
                return Result<bool>.Failure(EventErrors.EventNotFound(booking.EventId));

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
                return Result<bool>.Failure(seatsResult.Errors.ToArray());

            _uow.EnforceFencingToken(eventResult, eventResult.RowVersion);
            var result = await _uow.CommitAsync(cancellationToken);

            if (result <= 0)
                return Result<bool>.Failure(BookingErrors.CannotCancelBooking(bookingId.Value, booking.Status));

            await _cache.RemoveAsync(EventDetails(booking.EventId.Value), cancellationToken);

            return Result<bool>.Success(true);
        }
    }
}
