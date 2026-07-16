using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Bookings.Command.ConfirmBooking
{
    public class ConfirmBookingCommandHandler
        : ICommandHandler<ConfirmBookingCommand, bool>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeProvider _dateTimeProvider;
        private readonly ICurrentUserService _currentUserService;
        private readonly ICacheService _cache;

        public ConfirmBookingCommandHandler(
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

        public async Task<Result<bool>> Handle(ConfirmBookingCommand request, CancellationToken cancellationToken)
        {
            var bookingIdResult = BookingId.Create(request.BookingId);
            if (bookingIdResult.IsFailure)
                return Result<bool>.Failure(bookingIdResult.Errors.ToArray());

            var currentUserIdResult = _currentUserService.GetCurrentUserId();
            if (currentUserIdResult.IsFailure)
                return Result<bool>.Failure(currentUserIdResult.Errors.ToArray());

            return await ConcurrencyRetryHelper.ExecuteWithConcurrencyRetryAsync(
                () => AttemptConfirm(bookingIdResult.Value, currentUserIdResult.Value, cancellationToken),
                cancellationToken);
        }

        private async Task<Result<bool>> AttemptConfirm(
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

            if (userResult.Role != Role.Admin && userResult.Role != Role.Organizer)
                return Result<bool>.Failure(Error.Unauthorized(
                    "Booking.UnauthorizedConfirm",
                    "Only admins and organizers can confirm bookings."));

            var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;

            var confirmResult = booking.Confirm(utcNow);
            if (confirmResult.IsFailure)
                return Result<bool>.Failure(confirmResult.Errors.ToArray());

            var eventResult = await eventRepo.GetByIdAsync(booking.EventId, cancellationToken);
            if (eventResult is null)
                return Result<bool>.Failure(EventErrors.EventNotFound(booking.EventId));

            var confirmSeatsResult = eventResult.ConfirmReservation(
                booking.TicketTypeId,
                booking.Quantity,
                utcNow);
            if (confirmSeatsResult.IsFailure)
                return Result<bool>.Failure(confirmSeatsResult.Errors.ToArray());

            var rowsAffected = await uow.CommitAsync(cancellationToken);

            if (rowsAffected <= 0)
                return Result<bool>.Failure(BookingErrors.BookingConfirmationFailed());

            await _cache.RemoveAsync($"event:details:{booking.EventId.Value}", cancellationToken);

            return Result<bool>.Success(true);
        }
    }
}
