using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Payments;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Bookings.Command.CreateBooking
{
    public class CreateBookingCommandHandler : ICommandHandler<CreateBookingCommand, CreateBookingResponse>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeProvider _dateTimeProvider;
        private readonly ICurrentUserService _currentUserService;
        private readonly ICacheService _cache;
        private readonly IPaymentService _paymentService;

        private const int MAX_CONCURRENCY_RETRIES = 3;

        public CreateBookingCommandHandler(
            IServiceScopeFactory scopeFactory,
            TimeProvider dateTimeProvider,
            ICurrentUserService currentUserService,
            ICacheService cache,
            IPaymentService paymentService)
        {
            _scopeFactory = scopeFactory;
            _dateTimeProvider = dateTimeProvider;
            _currentUserService = currentUserService;
            _cache = cache;
            _paymentService = paymentService;
        }

        public async Task<Result<CreateBookingResponse>> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
        {
            var userIdResult = _currentUserService.GetCurrentUserId();
            if (userIdResult.IsFailure)
                return Result<CreateBookingResponse>.Failure(userIdResult.Errors.ToArray());

            var ticketTypeIdResult = TicketTypeId.Create(request.TicketTypeId);
            if (ticketTypeIdResult.IsFailure)
                return Result<CreateBookingResponse>.Failure(ticketTypeIdResult.Errors.ToArray());

            var eventIdResult = EventId.Create(request.EventId);
            if (eventIdResult.IsFailure)
                return Result<CreateBookingResponse>.Failure(eventIdResult.Errors.ToArray());

            for (var attempt = 1; attempt <= MAX_CONCURRENCY_RETRIES; attempt++)
            {
                try
                {
                    return await AttemptBooking(
                        userIdResult.Value,
                        eventIdResult.Value,
                        ticketTypeIdResult.Value,
                        request,
                        cancellationToken);
                }
                catch (ConcurrencyException) when (attempt < MAX_CONCURRENCY_RETRIES)
                {
                }
            }

            return Result<CreateBookingResponse>.Failure(BookingErrors.ConcurrencyConflict());
        }

        private async Task<Result<CreateBookingResponse>> AttemptBooking(
            UserId userId,
            EventId eventId,
            TicketTypeId ticketTypeId,
            CreateBookingCommand request,
            CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var eventRepo = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var eventResult = await eventRepo.GetByIdAsync(eventId, cancellationToken);
            if (eventResult is null)
                return Result<CreateBookingResponse>.Failure(EventErrors.EventNotFound(eventId));

            var ticketType = eventResult.TicketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType is null)
                return Result<CreateBookingResponse>.Failure(EventErrors.TicketTypeNotFound(request.TicketTypeId));

            var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;

            var reservationResult = eventResult.ReserveSeats(
                ticketTypeId, request.Quantity, utcNow);
            if (reservationResult.IsFailure)
                return Result<CreateBookingResponse>.Failure(reservationResult.Errors.ToArray());

            var booking = Booking.Create(
                userId, eventId, ticketTypeId,
                eventResult.EventName.Value,
                request.Quantity,
                ticketType.Price,
                request.PaymentMethod,
                utcNow);

            if (booking.IsFailure)
                return Result<CreateBookingResponse>.Failure(booking.Errors.ToArray());

            await bookingRepo.AddBookingAsync(booking.Value, cancellationToken);

            var rowsAffected = await uow.CommitAsync(cancellationToken);

            if (rowsAffected <= 0)
                return Result<CreateBookingResponse>.Failure(BookingErrors.BookingCreationFailed());

            await _cache.RemoveAsync($"event:details:{request.EventId}", cancellationToken);

            if (request.PaymentMethod == PaymentMethod.Instant)
            {
                var paymentResult = await _paymentService.InitiatePaymentAsync(
                    booking.Value.Id.Value,
                    booking.Value.ReferenceCode ?? booking.Value.Id.Value.ToString(),
                    booking.Value.TotalAmount,
                    booking.Value.Money.Currency,
                    cancellationToken);

                if (paymentResult.IsFailure)
                    return Result<CreateBookingResponse>.Failure(paymentResult.Errors.ToArray());

                return Result<CreateBookingResponse>.Success(new CreateBookingResponse(
                    booking.Value.Id.Value,
                    paymentResult.Value.PaymentUrl,
                    paymentResult.Value.ClientSecret));
            }

            return Result<CreateBookingResponse>.Success(new CreateBookingResponse(
                booking.Value.Id.Value,
                null,
                null));
        }
    }
}
