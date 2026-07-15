using Application.Abstractions.Caching;
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
    private readonly CreateBookingCommandHandler _handler;
    private readonly Event _sampleEvent;

    public CreateBookingHandlerTests()
    {
        _bookingRepo = Substitute.For<IBookingRepository>();
        _eventRepo = Substitute.For<IEventRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _cache = Substitute.For<ICacheService>();
        _paymentService = Substitute.For<IPaymentService>();
        _compensationRepo = Substitute.For<ICompensationLogRepository>();
        _timeProvider = TimeProvider.System;

        _currentUser.GetCurrentUserId().Returns(Result<UserId>.Success(UserId.FromDatabase(Guid.NewGuid())));
        _currentUser.IsAuthenticated.Returns(true);

        // Single event instance shared by mock and tests — guarantees ID stability
        _sampleEvent = CreateSampleEvent()!;

        _eventRepo.GetByIdAsync(Arg.Any<EventId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Event?>(_sampleEvent));

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
            _scopeFactory, _timeProvider, _currentUser, _cache, _paymentService);
    }

    private static Event? CreateSampleEvent()
    {
        var utcNow = DateTime.UtcNow;
        var address = Address.Create("Egypt", "Cairo", "Nile City", "11511", 30.0444, 31.2357).Value;

        var eventResult = Event.Create("Test Concert", 100, utcNow.AddDays(30),
            address, "A test concert", Domain.Aggregates.EventAggregate.Enums.EventType.Music, utcNow);
        if (eventResult.IsFailure) return null;

        var @event = eventResult.Value;
        @event.Publish(utcNow);
        var moneyResult = Money.FromDecimal(100m, "EGP");
        if (moneyResult.IsFailure) return null;
        @event.AddTicketType("General", moneyResult.Value, 50, utcNow);

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
}
