using Application.Abstractions.Caching;
using Application.Abstractions.Inventory;
using Application.Abstractions.Payments;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Application.Features.Bookings.Command.CreateBooking;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.Entities;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Errors;
using Domain.Primitives;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Eventy.Application.UnitTests.Features.Bookings.Commands;

public class CreateBookingHandlerTests
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;
    private readonly IPaymentService _paymentService;
    private readonly IBookingRepository _bookingRepo;
    private readonly IEventRepository _eventRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompensationLogRepository _compensationRepo;
    private readonly IInventoryReservationStrategy _optimisticStrategy;
    private readonly IInventoryReservationStrategy _atomicRedisStrategy;
    private readonly CreateBookingCommandHandler _handler;
    private readonly Event _sampleEvent;
    private Event _currentEvent;
    private Result<ReservationResult> _atomicRedisResult;
    private Result<ReservationResult> _optimisticResult;

    public CreateBookingHandlerTests()
    {
        _bookingRepo = Substitute.For<IBookingRepository>();
        _eventRepo = Substitute.For<IEventRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _cache = Substitute.For<ICacheService>();
        _paymentService = Substitute.For<IPaymentService>();
        _compensationRepo = Substitute.For<ICompensationLogRepository>();
        _optimisticStrategy = Substitute.For<IInventoryReservationStrategy>();
        _atomicRedisStrategy = Substitute.For<IInventoryReservationStrategy>();
        _timeProvider = TimeProvider.System;

        _currentUser.GetCurrentUserId().Returns(Result<UserId>.Success(UserId.FromDatabase(Guid.NewGuid())));
        _currentUser.IsAuthenticated.Returns(true);

        _sampleEvent = CreateSampleEvent()!;
        _currentEvent = _sampleEvent;
        _atomicRedisResult = Result<ReservationResult>.Success(ReservationResult.Success(48));
        _optimisticResult = Result<ReservationResult>.Success(ReservationResult.Success());

        _eventRepo.GetByIdAsync(Arg.Any<EventId>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Event?>(_currentEvent));

        _optimisticStrategy
            .ReserveAsync(Arg.Any<ReservationContext>(), Arg.Any<CancellationToken>())
            .Returns(_ => _optimisticResult);

        _atomicRedisStrategy
            .ReserveAsync(Arg.Any<ReservationContext>(), Arg.Any<CancellationToken>())
            .Returns(_ => _atomicRedisResult);

        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
        _unitOfWork.CommitWithoutEventsAsync(Arg.Any<CancellationToken>()).Returns(1);

        _paymentService.InitiatePaymentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<PaymentInitiationResult>.Success(
                new PaymentInitiationResult("https://pay.example.com/success", null)));

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IBookingRepository)).Returns(_bookingRepo);
        serviceProvider.GetService(typeof(IEventRepository)).Returns(_eventRepo);
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);
        serviceProvider.GetService(typeof(ICompensationLogRepository)).Returns(_compensationRepo);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);

        _handler = new CreateBookingCommandHandler(
            _scopeFactory, _timeProvider, _currentUser, _cache, _paymentService,
            _optimisticStrategy, _atomicRedisStrategy);
    }

    private static Event? CreateSampleEvent()
    {
        var utcNow = DateTime.UtcNow;
        var address = Address.Create("Egypt", "Cairo", "Nile City", "11511", 30.0444, 31.2357).Value;

        var eventResult = Event.Create("Test Concert", 100, utcNow.AddDays(30),
            address, "A test concert", Domain.Aggregates.EventAggregate.Enums.EventType.Music, utcNow);
        if (eventResult.IsFailure) return null;

        var @event = eventResult.Value;
        var moneyResult = Money.FromDecimal(100m, "EGP");
        if (moneyResult.IsFailure) return null;
        @event.AddTicketType("General", moneyResult.Value, 50, utcNow);
        @event.Publish(utcNow);

        return @event;
    }

    [Fact]
    public async Task Handle_WhenTicketAvailable_ShouldCreateBooking()
    {
        var ticketTypeId = _sampleEvent.TicketTypes.First().Id;

        var command = new CreateBookingCommand
        {
            EventId = _sampleEvent.Id.Value,
            TicketTypeId = ticketTypeId.Value,
            Quantity = 2,
            PaymentMethod = PaymentMethod.Instant,
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue("booking should succeed when tickets are available");
        await _bookingRepo.Received(1).AddBookingAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
        // Non-high-demand event => optimistic strategy invoked, atomic Redis not invoked
        await _optimisticStrategy.Received(1).ReserveAsync(
            Arg.Any<ReservationContext>(), Arg.Any<CancellationToken>());
        await _atomicRedisStrategy.DidNotReceive().ReserveAsync(
            Arg.Any<ReservationContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ShouldReturnFailure()
    {
        _currentUser.GetCurrentUserId().Returns(Result<UserId>.Failure(Error.Unauthorized("Auth.Failed", "Not authenticated")));
        _currentUser.IsAuthenticated.Returns(false);

        var command = new CreateBookingCommand
        {
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Quantity = 1,
            PaymentMethod = PaymentMethod.Instant,
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors[0].Type.Should().Be(ErrorType.Unauthorized);
        await _bookingRepo.DidNotReceive().AddBookingAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Pending-First pattern: even when payment initiation fails, the booking
    /// transaction MUST have committed first (Phase 1), and a durable
    /// compensation record MUST be staged (Phase 2). This replaces the old
    /// behaviour where failure rolled back the whole booking.
    /// </summary>
    [Fact]
    public async Task Handle_WhenPaymentInitiationFails_ShouldCommitBookingThenStageCompensation()
    {
        _paymentService.InitiatePaymentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<PaymentInitiationResult>.Failure(
                new Error("Payment.Failed", "Stripe is down", ErrorType.Failure)));

        var ticketTypeId = _sampleEvent.TicketTypes.First().Id;

        var command = new CreateBookingCommand
        {
            EventId = _sampleEvent.Id.Value,
            TicketTypeId = ticketTypeId.Value,
            Quantity = 2,
            PaymentMethod = PaymentMethod.Instant,
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        // Phase 1 — booking committed before payment was attempted.
        result.IsFailure.Should().BeTrue("payment initiation failed so the overall result is a failure");
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _bookingRepo.Received(1).AddBookingAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());

        // Phase 2 — durable compensation staged, not inline cancellation.
        _compensationRepo.Received(1).Add(Arg.Any<CompensationLogDto>());
        await _unitOfWork.Received(1).CommitWithoutEventsAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When Event.IsHighDemand is true, the handler MUST route through the
    /// AtomicRedisReservationStrategy instead of the optimistic one.
    /// </summary>
    [Fact]
    public async Task Handle_WhenEventIsHighDemand_ShouldUseAtomicRedisStrategy()
    {
        var highDemandEvent = CreateSampleEvent()!;
        highDemandEvent.SetHighDemandMode(true, DateTime.UtcNow);
        _currentEvent = highDemandEvent;
        var ticketTypeId = highDemandEvent.TicketTypes.First().Id;

        var command = new CreateBookingCommand
        {
            EventId = highDemandEvent.Id.Value,
            TicketTypeId = ticketTypeId.Value,
            Quantity = 2,
            PaymentMethod = PaymentMethod.Deferred,
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue("high-demand booking should succeed when Redis returns remaining >= 0");
        await _atomicRedisStrategy.Received(1).ReserveAsync(
            Arg.Any<ReservationContext>(), Arg.Any<CancellationToken>());
        await _optimisticStrategy.DidNotReceive().ReserveAsync(
            Arg.Any<ReservationContext>(), Arg.Any<CancellationToken>());
        await _bookingRepo.Received(1).AddBookingAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// If the AtomicRedisReservationStrategy reports a shortfall (sold out),
    /// the handler MUST short-circuit before creating a booking and must
    /// NOT call CommitAsync.
    /// </summary>
    [Fact]
    public async Task Handle_WhenHighDemandAndRedisSoldOut_ShouldNotCreateBooking()
    {
        var highDemandEvent = CreateSampleEvent()!;
        highDemandEvent.SetHighDemandMode(true, DateTime.UtcNow);
        _currentEvent = highDemandEvent;
        _atomicRedisResult = Result<ReservationResult>.Failure(
            EventErrors.RedisInventoryShortfall(2, 0));
        var ticketTypeId = highDemandEvent.TicketTypes.First().Id;

        var command = new CreateBookingCommand
        {
            EventId = highDemandEvent.Id.Value,
            TicketTypeId = ticketTypeId.Value,
            Quantity = 2,
            PaymentMethod = PaymentMethod.Instant,
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue("sold-out Redis counter must fail the booking");
        await _bookingRepo.DidNotReceive().AddBookingAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// If Redis is unreachable, the AtomicRedisReservationStrategy returns
    /// RedisInventoryUnavailable. The handler MUST surface that failure
    /// cleanly (no crash, no booking record, no commit).
    /// </summary>
    [Fact]
    public async Task Handle_WhenRedisUnavailable_ShouldReturnControlledFailure()
    {
        var highDemandEvent = CreateSampleEvent()!;
        highDemandEvent.SetHighDemandMode(true, DateTime.UtcNow);
        _currentEvent = highDemandEvent;
        _atomicRedisResult = Result<ReservationResult>.Failure(
            EventErrors.RedisInventoryUnavailable());
        var ticketTypeId = highDemandEvent.TicketTypes.First().Id;

        var command = new CreateBookingCommand
        {
            EventId = highDemandEvent.Id.Value,
            TicketTypeId = ticketTypeId.Value,
            Quantity = 1,
            PaymentMethod = PaymentMethod.Instant,
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue("Redis-down must surface as a controlled failure");
        result.Errors.Should().ContainSingle(e =>
            e.Code == "Event.RedisInventoryUnavailable");
        await _bookingRepo.DidNotReceive().AddBookingAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// If the optimistic strategy fails (e.g. insufficient seats), the
    /// handler MUST return the failure and skip booking creation.
    /// </summary>
    [Fact]
    public async Task Handle_WhenOptimisticStrategyFails_ShouldReturnFailure()
    {
        _optimisticResult = Result<ReservationResult>.Failure(
            BookingErrors.InsufficientSeats(5, 0));

        var ticketTypeId = _sampleEvent.TicketTypes.First().Id;

        var command = new CreateBookingCommand
        {
            EventId = _sampleEvent.Id.Value,
            TicketTypeId = ticketTypeId.Value,
            Quantity = 5,
            PaymentMethod = PaymentMethod.Instant,
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await _bookingRepo.DidNotReceive().AddBookingAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
