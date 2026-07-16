using Application.Abstractions.Caching;
using Application.Abstractions.Payments;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Application.Features.Bookings.Command.CreateBooking;
using Application.Features.Bookings.Command.ConfirmBookingFromWebhook;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Primitives;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Eventy.Application.UnitTests.Features.Bookings.Commands;

/// <summary>
/// Unit tests for the Pending-First + Durable Compensation pattern.
/// These verify the ordering invariants (commit-before-payment), idempotency
/// key contract, durable compensation staging, and webhook idempotency —
/// the critical safety properties of the refactor.
/// </summary>
public class CreateBookingPendingFirstTests
{
    private readonly IBookingRepository _bookingRepo;
    private readonly IEventRepository _eventRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompensationLogRepository _compensationRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;
    private readonly IPaymentService _paymentService;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly TimeProvider _timeProvider;
    private readonly Event _sampleEvent;
    private readonly CreateBookingCommandHandler _bookingHandler;
    private readonly IServiceScopeFactory _scopeFactory;

    public CreateBookingPendingFirstTests()
    {
        _bookingRepo = Substitute.For<IBookingRepository>();
        _eventRepo = Substitute.For<IEventRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _compensationRepo = Substitute.For<ICompensationLogRepository>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _cache = Substitute.For<ICacheService>();
        _paymentService = Substitute.For<IPaymentService>();
        _idempotencyStore = Substitute.For<IIdempotencyStore>();
        _timeProvider = TimeProvider.System;

        _currentUser.GetCurrentUserId()
            .Returns(Result<UserId>.Success(UserId.FromDatabase(Guid.NewGuid())));
        _currentUser.IsAuthenticated.Returns(true);

        _sampleEvent = CreateSampleEvent()!;
        _eventRepo.GetByIdAsync(Arg.Any<EventId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Event?>(_sampleEvent));

        // Phase-1 commit always succeeds by default; Phase-2 compensation commit too.
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
        _unitOfWork.CommitWithoutEventsAsync(Arg.Any<CancellationToken>()).Returns(1);

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IBookingRepository)).Returns(_bookingRepo);
        serviceProvider.GetService(typeof(IEventRepository)).Returns(_eventRepo);
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);
        serviceProvider.GetService(typeof(ICompensationLogRepository)).Returns(_compensationRepo);
        serviceProvider.GetService(typeof(IIdempotencyStore)).Returns(_idempotencyStore);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);

        _bookingHandler = new CreateBookingCommandHandler(
            _scopeFactory, _timeProvider, _currentUser, _cache, _paymentService);
    }

    private CreateBookingCommand InstantCommand() => new()
    {
        EventId = _sampleEvent.Id.Value,
        TicketTypeId = _sampleEvent.TicketTypes.First().Id.Value,
        Quantity = 2,
        PaymentMethod = PaymentMethod.Instant,
    };

    // =====================================================================
    //  Scenario A — Success Path: commit happens BEFORE payment, key passed
    // =====================================================================

    [Fact]
    public async Task ScenarioA_Success_CommitHappensBeforePaymentCall()
    {
        // Arrange — ordering probe: the call sequence is recorded so we can
        // assert CommitAsync preceded InitiatePaymentAsync.
        var callOrder = new List<string>();
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(Task.Run(() => { lock (callOrder) callOrder.Add("commit"); return 1; }));
        _paymentService.InitiatePaymentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.Run(() =>
            {
                lock (callOrder) callOrder.Add("payment");
                return Result<PaymentInitiationResult>.Success(
                    new PaymentInitiationResult("https://pay.example.com/success", null));
            }));

        // Act
        var result = await _bookingHandler.Handle(InstantCommand(), CancellationToken.None);

        // Assert — success + correct ordering
        result.IsSuccess.Should().BeTrue();
        result.Value.PaymentUrl.Should().NotBeNull();

        List<string> ordered;
        lock (callOrder) ordered = callOrder.ToList();
        string[] expectedOrder = ["commit", "payment"];
        ordered.Should().Equal(expectedOrder,
            "the booking MUST be committed before any external payment call — " +
            "this is the core Pending-First invariant that prevents the dual-write problem");
    }

    [Fact]
    public async Task ScenarioA_Success_IdempotencyKeyIsPassedToPaymentService()
    {
        // The handler must forward a deterministic, per-booking idempotency key
        // so that retried payment calls are deduplicated by the provider.
        string? receivedKey = null;
        _paymentService.InitiatePaymentAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Do<string>(k => receivedKey = k),
                Arg.Any<CancellationToken>())
            .Returns(Result<PaymentInitiationResult>.Success(
                new PaymentInitiationResult("https://pay.example.com/success", null)));

        var result = await _bookingHandler.Handle(InstantCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        receivedKey.Should().NotBeNull();
        receivedKey.Should().StartWith("payment-initiate:",
            "the idempotency key must be namespaced and deterministic");
        receivedKey.Should().MatchRegex(@"^payment-initiate:[0-9a-f\-]{36}$",
            "the key must embed the booking's GUID for provider-side deduplication");
    }

    [Fact]
    public async Task ScenarioA_Success_DeferredBookingSkipsPaymentAndCompensation()
    {
        // Deferred (Fawry/cash) bookings don't initiate an online payment, so
        // neither the gateway nor the compensation table should be touched.
        var command = InstantCommand() with { PaymentMethod = PaymentMethod.Deferred };

        var result = await _bookingHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PaymentUrl.Should().BeNull();
        await _paymentService.DidNotReceive().InitiatePaymentAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        _compensationRepo.DidNotReceive().Add(Arg.Any<CompensationLogDto>());
    }

    // =====================================================================
    //  Scenario B — External Failure: payment service returns Failure
    // =====================================================================

    [Fact]
    public async Task ScenarioB_ExternalFailure_BookingCommitsThenCompensationStagedDurably()
    {
        _paymentService.InitiatePaymentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<PaymentInitiationResult>.Failure(
                Error.Failure("Payment.GatewayDown", "Stripe returned 503")));

        var result = await _bookingHandler.Handle(InstantCommand(), CancellationToken.None);

        // Overall result is a failure for the caller (no payment URL).
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == "Booking.PaymentInitiationFailed");

        // Phase 1 — booking was committed first (no rollback).
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());

        // Phase 2 — durable compensation, NOT an inline cancel call.
        _compensationRepo.Received(1).Add(Arg.Is<CompensationLogDto>(dto =>
            dto.CompensationType == "CancelPayment" &&
            dto.IdempotencyKey.StartsWith("compensation:CancelPayment:")));
        await _unitOfWork.Received(1).CommitWithoutEventsAsync(Arg.Any<CancellationToken>());

        // No synchronous cancellation of the payment — that is the processor's job.
        await _paymentService.DidNotReceive().CancelPaymentAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // =====================================================================
    //  Scenario C — External Exception: payment service throws
    // =====================================================================

    [Fact]
    public async Task ScenarioC_ExternalException_CatchPathStagesCompensation()
    {
        // The provider call itself blew up (network reset, timeout, etc.).
        _paymentService.InitiatePaymentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<PaymentInitiationResult>>>(_ =>
                throw new InvalidOperationException("Connection reset by peer"));

        var result = await _bookingHandler.Handle(InstantCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue(
            "an exception from the gateway must surface as a failure, not crash the request");

        // Booking still committed (Phase 1 ran before the throw).
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());

        // Compensation staged via the catch path.
        _compensationRepo.Received(1).Add(Arg.Is<CompensationLogDto>(dto =>
            dto.CompensationType == "CancelPayment"));
        await _unitOfWork.Received(1).CommitWithoutEventsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScenarioC_CompensationPayloadCapturesFailureReason()
    {
        _paymentService.InitiatePaymentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<PaymentInitiationResult>.Failure(
                Error.Failure("Payment.GatewayDown", "503 Service Unavailable")));

        CompensationLogDto? captured = null;
        _compensationRepo.Add(Arg.Do<CompensationLogDto>(dto => captured = dto));

        await _bookingHandler.Handle(InstantCommand(), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Payload.Should().Contain("503 Service Unavailable",
            "the compensation payload must record WHY the payment failed so the " +
            "processor and operators can diagnose later");
        captured.RetryCount.Should().Be(0);
        captured.ProcessedOnUtc.Should().BeNull();
    }

    // =====================================================================
    //  Scenario E — Webhook Idempotency (ConfirmBookingFromWebhook)
    // =====================================================================

    [Fact]
    public async Task ScenarioE_WebhookIdempotency_DuplicateEventDoesNotDoubleConfirm()
    {
        // Wire up the webhook handler with a tracked booking + event.
        // The event must have seats reserved for ConfirmReservation to succeed.
        var ticketTypeId = _sampleEvent.TicketTypes.First().Id;
        _sampleEvent.ReserveSeats(ticketTypeId, quantity: 1, DateTime.UtcNow);

        var booking = Booking.Create(
            UserId.FromDatabase(Guid.NewGuid()),
            _sampleEvent.Id,
            ticketTypeId,
            "Pending-First Event",
            1,
            Money.FromDecimal(100m, "EGP").Value,
            PaymentMethod.Instant,
            DateTime.UtcNow).Value;

        _bookingRepo.GetByIdAsync(Arg.Any<BookingId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Booking?>(booking));
        _eventRepo.GetByIdAsync(Arg.Any<EventId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Event?>(_sampleEvent));

        // First delivery — not yet processed.
        _idempotencyStore.IsProcessedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var webhookHandler = new ConfirmBookingFromWebhookCommandHandler(
            _scopeFactory, _timeProvider, _cache);

        var stripeEventId = "evt_test_" + Guid.NewGuid().ToString("N");
        var command = new ConfirmBookingFromWebhookCommand(booking.Id.Value, stripeEventId);

        var first = await webhookHandler.Handle(command, CancellationToken.None);
        first.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatusEnum.Confirmed);

        // Second delivery of the SAME event id — store now reports processed.
        _idempotencyStore.IsProcessedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Received(1) assertions: clear the previous call counts before the duplicate.
        _unitOfWork.ClearReceivedCalls();

        var second = await webhookHandler.Handle(command, CancellationToken.None);

        second.IsSuccess.Should().BeTrue(
            "a duplicate webhook must be acknowledged as success so Stripe stops retrying");
        // The second delivery must NOT open a new transaction — it is short-circuited
        // by the idempotency guard before any write.
        await _unitOfWork.DidNotReceiveWithAnyArgs().CommitAsync(Arg.Any<CancellationToken>());
    }

    #region Sample event factory

    private static Event? CreateSampleEvent()
    {
        var utcNow = DateTime.UtcNow;
        var address = Address.Create("Egypt", "Cairo", "Nile City", "11511", 30.0444, 31.2357).Value;
        var eventResult = Event.Create("Pending-First Concert", 100, utcNow.AddDays(30),
            address, "test", Domain.Aggregates.EventAggregate.Enums.EventType.Music, utcNow);
        if (eventResult.IsFailure) return null;
        var @event = eventResult.Value;
        @event.Publish(utcNow);
        @event.AddTicketType("General", Money.FromDecimal(100m, "EGP").Value, 50, utcNow);
        return @event;
    }

    #endregion
}
