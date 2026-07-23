using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Application.Abstractions.Persistence;
using Application.Features.Bookings.Command.ConfirmDeferredPayment;
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

public class ConfirmDeferredPaymentHandlerTests
{
    private readonly IBookingRepository _bookingRepo;
    private readonly IEventRepository _eventRepo;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;
    private readonly ICacheService _cache;
    private readonly ConfirmDeferredPaymentCommandHandler _handler;
    private readonly Event _sampleEvent;

    public ConfirmDeferredPaymentHandlerTests()
    {
        _bookingRepo = Substitute.For<IBookingRepository>();
        _eventRepo = Substitute.For<IEventRepository>();
        _uow = Substitute.For<IUnitOfWork>();
        _timeProvider = TimeProvider.System;
        _cache = Substitute.For<ICacheService>();

        _sampleEvent = CreateSampleEvent()!;
        _eventRepo.GetByIdAsync(Arg.Any<EventId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Event?>(_sampleEvent));
        _uow.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IBookingRepository)).Returns(_bookingRepo);
        serviceProvider.GetService(typeof(IEventRepository)).Returns(_eventRepo);
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_uow);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        _handler = new ConfirmDeferredPaymentCommandHandler(
            _timeProvider, _cache, _bookingRepo, _eventRepo, _uow);
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

    private Booking CreateDeferredBooking()
    {
        var ticketType = _sampleEvent.TicketTypes.First();
        var moneyResult = Money.FromDecimal(100m, "EGP");
        var userId = UserId.FromDatabase(Guid.NewGuid());
        var bookingResult = Booking.Create(
            userId,
            _sampleEvent.Id,
            ticketType.Id,
            _sampleEvent.EventName.Value,
            2,
            moneyResult.Value,
            PaymentMethod.Deferred,
            DateTime.UtcNow);
        return bookingResult.Value;
    }

    [Fact]
    public async Task Handle_WhenReferenceCodeNotFound_ShouldReturnFailure()
    {
        _bookingRepo.GetByReferenceCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Booking?>(null));

        var result = await _handler.Handle(
            new ConfirmDeferredPaymentCommand("FAW-DEADBEEF"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors[0].Code.Should().Be("Booking.ReferenceCodeNotFound");
    }

    [Fact]
    public async Task Handle_WhenNotDeferredBooking_ShouldReturnFailure()
    {
        var booking = CreateDeferredBooking();
        var instantResult = Booking.Create(
            booking.UserId, booking.EventId, booking.TicketTypeId,
            booking.EventTitle, 2,
            Money.FromDecimal(100m, "EGP").Value,
            PaymentMethod.Instant, DateTime.UtcNow);
        var instantBooking = instantResult.Value;

        _bookingRepo.GetByReferenceCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Booking?>(instantBooking));

        var result = await _handler.Handle(
            new ConfirmDeferredPaymentCommand("FAW-DEADBEEF"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors[0].Code.Should().Be("Booking.NotDeferred");
    }

    [Fact]
    public async Task Handle_WhenBookingNotPending_ShouldReturnFailure()
    {
        var booking = CreateDeferredBooking();
        booking.Confirm(DateTime.UtcNow);

        _bookingRepo.GetByReferenceCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Booking?>(booking));

        var result = await _handler.Handle(
            new ConfirmDeferredPaymentCommand("FAW-DEADBEEF"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors[0].Code.Should().Be("Booking.BookingNotPending");
    }

    [Fact]
    public async Task Handle_WhenValidDeferredBooking_ShouldConfirmAndCommit()
    {
        var booking = CreateDeferredBooking();
        var ticketType = _sampleEvent.TicketTypes.First();
        _sampleEvent.ReserveSeats(ticketType.Id, booking.Quantity, DateTime.UtcNow);

        _bookingRepo.GetByReferenceCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Booking?>(booking));

        var result = await _handler.Handle(
            new ConfirmDeferredPaymentCommand("FAW-DEADBEEF"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatusEnum.Confirmed);
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync(
            $"event:details:{_sampleEvent.Id.Value}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCommitReturnsZero_ShouldReturnFailure()
    {
        var booking = CreateDeferredBooking();
        var ticketType = _sampleEvent.TicketTypes.First();
        _sampleEvent.ReserveSeats(ticketType.Id, booking.Quantity, DateTime.UtcNow);

        _bookingRepo.GetByReferenceCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Booking?>(booking));
        _uow.CommitAsync(Arg.Any<CancellationToken>()).Returns(0);

        var result = await _handler.Handle(
            new ConfirmDeferredPaymentCommand("FAW-DEADBEEF"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors[0].Code.Should().Be("Booking.ConfirmationFailed");
    }
}
