using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Abstractions.Security;
using Application.Features.Bookings.Command.CancelBooking;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.BookingAggregate.ValueObject;
using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Aggregates.UserAggregate;
using Domain.Aggregates.UserAggregate.ValueObject;
using Domain.Common;
using Domain.Primitives;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Eventy.Application.UnitTests.Features.Bookings.Commands;

public class CancelBookingHandlerTests
{
    private readonly IBookingRepository _bookingRepo;
    private readonly IEventRepository _eventRepo;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;
    private readonly IUserRepository _userRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;
    private readonly CancelBookingCommandHandler _handler;
    private readonly Event _sampleEvent;
    private readonly UserId _userId;

    public CancelBookingHandlerTests()
    {
        _bookingRepo = Substitute.For<IBookingRepository>();
        _eventRepo = Substitute.For<IEventRepository>();
        _uow = Substitute.For<IUnitOfWork>();
        _timeProvider = TimeProvider.System;
        _userRepo = Substitute.For<IUserRepository>();
        _currentUser = Substitute.For<ICurrentUserService>();
        _cache = Substitute.For<ICacheService>();

        _userId = UserId.FromDatabase(Guid.NewGuid());
        _currentUser.GetCurrentUserId().Returns(Result<UserId>.Success(_userId));

        _sampleEvent = CreateSampleEvent()!;
        _eventRepo.GetByIdAsync(Arg.Any<EventId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Event?>(_sampleEvent));
        _uow.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IBookingRepository)).Returns(_bookingRepo);
        serviceProvider.GetService(typeof(IEventRepository)).Returns(_eventRepo);
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_uow);
        serviceProvider.GetService(typeof(IUserRepository)).Returns(_userRepo);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        _handler = new CancelBookingCommandHandler(
            _timeProvider, _currentUser, _cache, _bookingRepo, _eventRepo, _userRepo, _uow);
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

    private User CreateAttendeeUser(UserId userId)
    {
        var emailResult = Email.Create($"test{Guid.NewGuid():N}@example.com");
        return User.Create(
            "Test", "User", emailResult.Value, "hashedpassword",
            Role.Attendee, DateTime.UtcNow).Value!;
    }

    private Booking CreatePendingBooking(UserId userId)
    {
        var ticketType = _sampleEvent.TicketTypes.First();
        var moneyResult = Money.FromDecimal(100m, "EGP");
        var bookingResult = Booking.Create(
            userId, _sampleEvent.Id, ticketType.Id,
            _sampleEvent.EventName.Value, 2,
            moneyResult.Value, PaymentMethod.Instant, DateTime.UtcNow);
        return bookingResult.Value;
    }

    [Fact]
    public async Task Handle_WhenBookingNotFound_ShouldReturnFailure()
    {
        _bookingRepo.GetByIdAsync(Arg.Any<BookingId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Booking?>(null));

        var result = await _handler.Handle(
            new CancelBookingCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors[0].Code.Should().Be("Booking.BookingNotFound");
    }

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ShouldReturnFailure()
    {
        _currentUser.GetCurrentUserId().Returns(
            Result<UserId>.Failure(Error.Unauthorized("Auth.Failed", "Not authenticated")));

        var booking = CreatePendingBooking(_userId);
        _bookingRepo.GetByIdAsync(Arg.Any<BookingId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Booking?>(booking));

        var result = await _handler.Handle(
            new CancelBookingCommand(booking.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenAttendeeCancelsOwnPendingBooking_ShouldSucceed()
    {
        var user = CreateAttendeeUser(_userId);
        _userRepo.GetByIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<User?>(user));

        var booking = CreatePendingBooking(_userId);
        var ticketType = _sampleEvent.TicketTypes.First();
        _sampleEvent.ReserveSeats(ticketType.Id, booking.Quantity, DateTime.UtcNow);

        _bookingRepo.GetByIdAsync(Arg.Any<BookingId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Booking?>(booking));

        var result = await _handler.Handle(
            new CancelBookingCommand(booking.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatusEnum.Cancelled);
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAttendeeCancelsOthersBooking_ShouldReturnUnauthorized()
    {
        var user = CreateAttendeeUser(_userId);
        _userRepo.GetByIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<User?>(user));

        var otherUserId = UserId.FromDatabase(Guid.NewGuid());
        var booking = CreatePendingBooking(otherUserId);
        _bookingRepo.GetByIdAsync(Arg.Any<BookingId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Booking?>(booking));

        var result = await _handler.Handle(
            new CancelBookingCommand(booking.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors[0].Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_WhenAttendeeCancelsConfirmedBooking_ShouldReturnFailure()
    {
        var user = CreateAttendeeUser(_userId);
        _userRepo.GetByIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<User?>(user));

        var booking = CreatePendingBooking(_userId);
        booking.Confirm(DateTime.UtcNow);

        _bookingRepo.GetByIdAsync(Arg.Any<BookingId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Booking?>(booking));

        var result = await _handler.Handle(
            new CancelBookingCommand(booking.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors[0].Code.Should().Be("Booking.CannotCancelBooking");
    }

    [Fact]
    public async Task Handle_WhenAdminCancelsConfirmedBooking_ShouldRefundSeats()
    {
        var adminId = UserId.FromDatabase(Guid.NewGuid());
        _currentUser.GetCurrentUserId().Returns(Result<UserId>.Success(adminId));

        var adminEmail = Email.Create($"admin{Guid.NewGuid():N}@example.com");
        var admin = User.Create(
            "Admin", "User", adminEmail.Value, "hashed", Role.Admin, DateTime.UtcNow).Value!;
        _userRepo.GetByIdAsync(adminId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<User?>(admin));

        var booking = CreatePendingBooking(_userId);
        var ticketType = _sampleEvent.TicketTypes.First();
        _sampleEvent.ReserveSeats(ticketType.Id, booking.Quantity, DateTime.UtcNow);
        booking.Confirm(DateTime.UtcNow);
        _sampleEvent.ConfirmReservation(ticketType.Id, booking.Quantity, DateTime.UtcNow);

        _bookingRepo.GetByIdAsync(Arg.Any<BookingId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Booking?>(booking));

        var result = await _handler.Handle(
            new CancelBookingCommand(booking.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatusEnum.Cancelled);
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCommitReturnsZero_ShouldReturnFailure()
    {
        var user = CreateAttendeeUser(_userId);
        _userRepo.GetByIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<User?>(user));

        var booking = CreatePendingBooking(_userId);
        var ticketType = _sampleEvent.TicketTypes.First();
        _sampleEvent.ReserveSeats(ticketType.Id, booking.Quantity, DateTime.UtcNow);

        _bookingRepo.GetByIdAsync(Arg.Any<BookingId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Booking?>(booking));
        _uow.CommitAsync(Arg.Any<CancellationToken>()).Returns(0);

        var result = await _handler.Handle(
            new CancelBookingCommand(booking.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
