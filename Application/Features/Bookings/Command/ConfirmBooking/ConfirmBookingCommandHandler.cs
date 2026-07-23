using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using static Application.Abstractions.Caching.CacheKeys;

namespace Application.Features.Bookings.Command.ConfirmBooking
{
    public class ConfirmBookingCommandHandler
        : ICommandHandler<ConfirmBookingCommand, bool>
    {
        private readonly TimeProvider _dateTimeProvider;
        private readonly ICurrentUserService _currentUserService;
        private readonly ICacheService _cache;
        private readonly IBookingRepository _bookingRepo;
        private readonly IEventRepository _eventRepo;
        private readonly IUnitOfWork _uow;

        public ConfirmBookingCommandHandler(
            TimeProvider dateTimeProvider,
            ICurrentUserService currentUserService,
            ICacheService cache,
            IBookingRepository bookingRepo,
            IEventRepository eventRepo,
            IUnitOfWork uow)
        {
            _dateTimeProvider = dateTimeProvider;
            _currentUserService = currentUserService;
            _cache = cache;
            _bookingRepo = bookingRepo;
            _eventRepo = eventRepo;
            _uow = uow;
        }

        public async Task<Result<bool>> Handle(ConfirmBookingCommand request, CancellationToken cancellationToken)
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

            var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;

            var confirmResult = booking.Confirm(utcNow);
            if (confirmResult.IsFailure)
                return Result<bool>.Failure(confirmResult.Errors.ToArray());

            var eventResult = await _eventRepo.GetByIdAsync(booking.EventId, cancellationToken);
            if (eventResult is null)
                return Result<bool>.Failure(EventErrors.EventNotFound(booking.EventId));

            var confirmSeatsResult = eventResult.ConfirmReservation(
                booking.TicketTypeId,
                booking.Quantity,
                utcNow);
            if (confirmSeatsResult.IsFailure)
                return Result<bool>.Failure(confirmSeatsResult.Errors.ToArray());

            _uow.EnforceFencingToken(eventResult, eventResult.RowVersion);
            var rowsAffected = await _uow.CommitAsync(cancellationToken);

            if (rowsAffected <= 0)
                return Result<bool>.Failure(BookingErrors.BookingConfirmationFailed());

            await _cache.RemoveAsync(EventDetails(booking.EventId.Value), cancellationToken);

            return Result<bool>.Success(true);
        }
    }
}
