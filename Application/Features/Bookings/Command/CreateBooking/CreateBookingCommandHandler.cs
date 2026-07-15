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
using System.Text.Json;

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
            var compensationRepo = scope.ServiceProvider.GetRequiredService<ICompensationLogRepository>();

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

            // ===== PHASE 1: Atomic Commit — Save Booking as Pending (seats reserved) =====
            int rowsAffected;
            try
            {
                rowsAffected = await uow.CommitAsync(cancellationToken);
            }
            catch (ConcurrencyException)
            {
                throw;
            }

            if (rowsAffected <= 0)
                return Result<CreateBookingResponse>.Failure(BookingErrors.BookingCreationFailed());

            await _cache.RemoveAsync($"event:details:{request.EventId}", cancellationToken);

            // ===== PHASE 2: Post-Commit Payment Initiation (Instant only) =====
            // The booking is now durably persisted as Pending. If the payment call
            // fails or the process crashes, the BookingExpirationJob will eventually
            // expire it and release the reserved seats. No dual-write problem.

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
                        compensationRepo,
                        uow,
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
                // Payment call threw unexpectedly — stage durable compensation
                // instead of inline cancellation. The CompensationProcessor will
                // retry the cancellation asynchronously.
                await StageCompensationDurally(
                    compensationRepo,
                    uow,
                    booking.Value.Id.Value,
                    "CancelPayment",
                    ex.Message,
                    utcNow,
                    cancellationToken);

                return Result<CreateBookingResponse>.Failure(
                    BookingErrors.PaymentInitiationFailed(ex.Message));
            }
        }

        private static async Task StageCompensationDurally(
            ICompensationLogRepository compensationRepo,
            IUnitOfWork uow,
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

            compensationRepo.Add(compensationLog);

            // Separate scope commit — the original booking transaction already
            // committed successfully. This is a second atomic write to record the
            // compensation intent. If THIS commit fails, the PaymentReconciliationJob
            // will still find the orphaned Stripe session by polling past-hold bookings.
            await uow.CommitWithoutEventsAsync(ct);
        }
    }
}
