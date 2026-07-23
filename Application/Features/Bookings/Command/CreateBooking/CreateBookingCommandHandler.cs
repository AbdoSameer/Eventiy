using Application.Abstractions.Caching;
using Application.Abstractions.Inventory;
using Application.Abstractions.Messaging;
using Application.Abstractions.Payments;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.BookingAggregate.Enums;
using static Application.Abstractions.Caching.CacheKeys;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Application.Features.Bookings.Command.CreateBooking
{
    public class CreateBookingCommandHandler : ICommandHandler<CreateBookingCommand, CreateBookingResponse>
    {
        private readonly TimeProvider _dateTimeProvider;
        private readonly ICurrentUserService _currentUserService;
        private readonly ICacheService _cache;
        private readonly IPaymentService _paymentService;
        private readonly IInventoryReservationStrategy _optimisticStrategy;
        private readonly IInventoryReservationStrategy _atomicRedisStrategy;
        private readonly IBookingRepository _bookingRepo;
        private readonly IEventRepository _eventRepo;
        private readonly IUnitOfWork _uow;
        private readonly ICompensationLogRepository _compensationRepo;

        public CreateBookingCommandHandler(
            TimeProvider dateTimeProvider,
            ICurrentUserService currentUserService,
            ICacheService cache,
            IPaymentService paymentService,
            [FromKeyedServices(Application.DependencyInjection.OptimisticStrategyKey)]
                IInventoryReservationStrategy optimisticStrategy,
            [FromKeyedServices(Application.DependencyInjection.AtomicRedisStrategyKey)]
                IInventoryReservationStrategy atomicRedisStrategy,
            IBookingRepository bookingRepo,
            IEventRepository eventRepo,
            IUnitOfWork uow,
            ICompensationLogRepository compensationRepo)
        {
            _dateTimeProvider = dateTimeProvider;
            _currentUserService = currentUserService;
            _cache = cache;
            _paymentService = paymentService;
            _optimisticStrategy = optimisticStrategy;
            _atomicRedisStrategy = atomicRedisStrategy;
            _bookingRepo = bookingRepo;
            _eventRepo = eventRepo;
            _uow = uow;
            _compensationRepo = compensationRepo;
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

            var userId = userIdResult.Value;
            var eventId = eventIdResult.Value;
            var ticketTypeId = ticketTypeIdResult.Value;

            var eventResult = await _eventRepo.GetByIdAsync(eventId, cancellationToken);
            if (eventResult is null)
                return Result<CreateBookingResponse>.Failure(EventErrors.EventNotFound(eventId));

            var fencingToken = eventResult.RowVersion;

            var ticketType = eventResult.TicketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
            if (ticketType is null)
                return Result<CreateBookingResponse>.Failure(EventErrors.TicketTypeNotFound(request.TicketTypeId));

            var utcNow = _dateTimeProvider.GetUtcNow().UtcDateTime;

            var strategy = eventResult.IsHighDemand
                ? _atomicRedisStrategy
                : _optimisticStrategy;

            var reservationResult = await strategy.ReserveAsync(
                new ReservationContext(
                    eventResult,
                    ticketTypeId,
                    request.Quantity,
                    utcNow),
                cancellationToken);

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

            await _bookingRepo.AddBookingAsync(booking.Value, cancellationToken);

            _uow.EnforceFencingToken(eventResult, fencingToken);

            var rowsAffected = await _uow.CommitAsync(cancellationToken);

            if (rowsAffected <= 0)
                return Result<CreateBookingResponse>.Failure(BookingErrors.BookingCreationFailed());

            await _cache.RemoveAsync(EventDetails(request.EventId), cancellationToken);

            if (request.PaymentMethod != PaymentMethod.Instant)
                return Result<CreateBookingResponse>.Success(new CreateBookingResponse(
                    booking.Value.Id.Value,
                    null,
                    null));

            string idempotencyKey = $"payment-initiate:{booking.Value.Id.Value}";

            try
            {
                var paymentResult = await _paymentService.InitiatePaymentAsync(
                    booking.Value.Id.Value,
                    booking.Value.ReferenceCode ?? booking.Value.Id.Value.ToString(),
                    booking.Value.TotalAmount,
                    booking.Value.Money.Currency,
                    idempotencyKey,
                    cancellationToken);

                if (paymentResult.IsFailure)
                {
                    await StageCompensationDurally(
                        booking.Value.Id.Value,
                        "CancelPayment",
                        paymentResult.Errors.Select(e => e.Message).Aggregate((a, b) => $"{a}; {b}"),
                        utcNow,
                        cancellationToken);

                    return Result<CreateBookingResponse>.Failure(
                        BookingErrors.PaymentInitiationFailed(
                            paymentResult.Errors.Select(e => e.Message).FirstOrDefault() ?? "Unknown error"));
                }

                return Result<CreateBookingResponse>.Success(new CreateBookingResponse(
                    booking.Value.Id.Value,
                    paymentResult.Value.PaymentUrl,
                    paymentResult.Value.ClientSecret));
            }
            catch (Exception ex)
            {
                await StageCompensationDurally(
                    booking.Value.Id.Value,
                    "CancelPayment",
                    ex.Message,
                    utcNow,
                    cancellationToken);

                return Result<CreateBookingResponse>.Failure(
                    BookingErrors.PaymentInitiationFailed(ex.Message));
            }
        }

        private async Task StageCompensationDurally(
            Guid bookingId,
            string compensationType,
            string failureReason,
            DateTime utcNow,
            CancellationToken ct)
        {
            var compensationLog = new CompensationLogDto(
                Id: Guid.NewGuid(),
                BookingId: bookingId,
                CompensationType: compensationType,
                Payload: JsonSerializer.Serialize(new
                {
                    BookingId = bookingId,
                    Reason = failureReason,
                    OccurredAt = utcNow
                }),
                OccurredOnUtc: utcNow,
                IdempotencyKey: $"compensation:{compensationType}:{bookingId}",
                ProcessedOnUtc: null,
                Error: null,
                RetryCount: 0,
                NextRetryOnUtc: null);

            _compensationRepo.Add(compensationLog);

            await _uow.CommitWithoutEventsAsync(ct);
        }
    }
}
