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
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Bookings.Command.CancelBooking
{
    public class CancelBookingCommandHandler : ICommandHandler<CancelBookingCommand, bool>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeProvider _dateTimeProvider;
        private readonly ICurrentUserService _currentUserService;
        private readonly ICacheService _cache;

        public CancelBookingCommandHandler(
            IServiceScopeFactory scopeFactory,
            TimeProvider dateTimeProvider,
            ICurrentUserService currentUserService,
            ICacheService cache)
        {
            _scopeFactory = scopeFactory;
            _dateTimeProvider = dateTimeProvider;
            _currentUserService = currentUserService;
            _cache = cache;
        }

        public async Task<Result<bool>> Handle(CancelBookingCommand request, CancellationToken cancellationToken)
        {
            var bookingIdResult = BookingId.Create(request.BookingId);
            if (bookingIdResult.IsFailure)
                return Result<bool>.Failure(bookingIdResult.Errors.ToArray());

            var currentUserIdResult = _currentUserService.GetCurrentUserId();
            if (currentUserIdResult.IsFailure)
                return Result<bool>.Failure(currentUserIdResult.Errors.ToArray());

            return await ConcurrencyRetryHelper.ExecuteWithConcurrencyRetryAsync(
                () => AttemptCancel(bookingIdResult.Value, currentUserIdResult.Value, cancellationToken),
                cancellationToken);
        }

        private async Task<Result<bool>> AttemptCancel(
            BookingId bookingId,
            UserId currentUserId,
            CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var eventRepo = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var booking = await bookingRepo.GetByIdAsync(bookingId, cancellationToken);
            if (booking is null)
                return Result<bool>.Failure(BookingErrors.BookingNotFound(bookingId));

            var userResult = await userRepo.GetByIdAsync(currentUserId, cancellationToken);
            if (userResult is null)
                return Result<bool>.Failure(UserErrors.NotFound());

            var isAdminOrOrg = userResult.Role == Domain.Aggregates.UserAggregate.ValueObject.Role.Admin
                            || userResult.Role == Domain.Aggregates.UserAggregate.ValueObject.Role.Organizer;

            var isOwnBooking = booking.UserId == currentUserId;

            if (!isAdminOrOrg && !isOwnBooking)
                return Result<bool>.Failure(Error.Unauthorized(
                    "Booking.UnauthorizedCancel",
                    "You can only cancel your own bookings."));

            if (!isAdminOrOrg && isOwnBooking && booking.Status != BookingStatusEnum.Pending)
                return Result<bool>.Failure(BookingErrors.CannotCancelBooking(bookingId, booking.Status));

            var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;
            var wasConfirmed = booking.Status == BookingStatusEnum.Confirmed;

            var cancelResult = booking.Cancel(utcNow);
            if (cancelResult.IsFailure)
                return Result<bool>.Failure(cancelResult.Errors.ToArray());

            var eventResult = await eventRepo.GetByIdAsync(booking.EventId, cancellationToken);
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

            uow.EnforceFencingToken(eventResult, eventResult.RowVersion);
            var result = await uow.CommitAsync(cancellationToken);

            if (result <= 0)
                return Result<bool>.Failure(BookingErrors.CannotCancelBooking(bookingId, booking.Status));

            await _cache.RemoveAsync($"event:details:{booking.EventId.Value}", cancellationToken);

            return Result<bool>.Success(true);
        }
    }
}
