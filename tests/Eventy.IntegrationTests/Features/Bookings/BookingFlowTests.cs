using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Outbox;
using Application.Abstractions.Persistence;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Eventy.IntegrationTests.Assertions;
using Eventy.IntegrationTests.Fixtures;
using Eventy.Testing.Foundation.Fakes;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using EventId = Domain.Aggregates.EventAggregate.ValueObject.EventId;

namespace Eventy.IntegrationTests.Features.Bookings;

[Collection("Integration")]
public class BookingFlowTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly HttpClient _client;

    public BookingFlowTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    public async Task InitializeAsync() => await _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(Guid EventId, Guid TicketTypeId)> SeedEventAsync(int capacity = 50, int ticketCapacity = 50)
    {
        return await _fixture.SeedPublishedEventAsync(eventCapacity: capacity, ticketCapacity: ticketCapacity);
    }

    [Fact]
    public async Task CreateBooking_WhenPaymentFails_ShouldNotCreateBooking()
    {
        var (eventId, ticketTypeId) = await SeedEventAsync();

        var scope = _fixture.Factory.Services.CreateScope();
        var ps = scope.ServiceProvider.GetRequiredService<FakePaymentService>();
        ps.SetFailMode(true);

        var response = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 1,
            PaymentMethod = 0,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "payment failure should return error");

        await using var db = _fixture.CreateDbContext();
        var bookingCount = await db.Db.Bookings
            .CountAsync(b => b.EventId == EventId.FromDatabase(eventId));
        bookingCount.Should().Be(0, "no booking should persist when payment fails");

        ps.SetFailMode(false);
    }

    [Fact]
    public async Task DeferredPayment_CreateThenConfirmByReferenceCode_ShouldConfirmBooking()
    {
        var (eventId, ticketTypeId) = await SeedEventAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 2,
            PaymentMethod = 1,
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var db = _fixture.CreateDbContext();
        var booking = await db.Db.Bookings
            .FirstOrDefaultAsync(b => b.EventId == EventId.FromDatabase(eventId));
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatusEnum.Pending);
        booking.ReferenceCode.Should().NotBeNullOrEmpty();

        var refCode = booking.ReferenceCode!;

        var confirmResponse = await _client.PostAsJsonAsync("/api/booking/confirm-deferred", new
        {
            ReferenceCode = refCode,
        });

        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db2 = _fixture.CreateDbContext();
        var confirmedBooking = await db2.Db.Bookings
            .FirstOrDefaultAsync(b => b.ReferenceCode == refCode);
        confirmedBooking.Should().NotBeNull();
        confirmedBooking!.Status.Should().Be(BookingStatusEnum.Confirmed);
    }

    [Fact]
    public async Task CreateBooking_WhenInsufficientCapacity_ShouldReturnBadRequest()
    {
        var (eventId, ticketTypeId) = await SeedEventAsync(capacity: 5, ticketCapacity: 5);

        var response = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 10,
            PaymentMethod = 0,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "requesting more seats than available should fail");

        await using var db = _fixture.CreateDbContext();
        var bookingCount = await db.Db.Bookings
            .CountAsync(b => b.EventId == EventId.FromDatabase(eventId));
        bookingCount.Should().Be(0, "no booking should be created");
    }

    [Fact]
    public async Task BookingExpiration_WhenHoldExpires_ShouldExpireAndReleaseSeats()
    {
        var (eventId, ticketTypeId) = await SeedEventAsync(capacity: 10, ticketCapacity: 10);

        var createResponse = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 3,
            PaymentMethod = 1,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var dbBefore = _fixture.CreateDbContext();
        var bookingBefore = await dbBefore.Db.Bookings
            .FirstOrDefaultAsync(b => b.EventId == EventId.FromDatabase(eventId));
        bookingBefore.Should().NotBeNull();
        bookingBefore!.Status.Should().Be(BookingStatusEnum.Pending);

        var eventBefore = await dbBefore.Db.Events
            .FirstOrDefaultAsync(e => e.Id == EventId.FromDatabase(eventId));
        var ticketTypeBefore = eventBefore!.TicketTypes.First();
        ticketTypeBefore.ReservedCount.Should().Be(3);

        var holdExpiresAt = bookingBefore.HoldExpiresAt!.Value;
        var now = holdExpiresAt.AddSeconds(1);

        using var scope = _fixture.Factory.Services.CreateScope();
        var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var eventRepo = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var expiredBookings = await bookingRepo.GetExpiredPendingBookingsAsync(now, 100, CancellationToken.None);
        expiredBookings.Should().HaveCount(1);

        var expired = expiredBookings[0];
        var expireResult = expired.Expire(now);
        expireResult.IsSuccess.Should().BeTrue();

        var evt = await eventRepo.GetByIdAsync(expired.EventId, CancellationToken.None);
        evt.Should().NotBeNull();
        var releaseResult = evt!.ReleaseSeats(expired.TicketTypeId, expired.Quantity, now);
        releaseResult.IsSuccess.Should().BeTrue();

        await uow.CommitAsync(CancellationToken.None);

        await using var dbAfter = _fixture.CreateDbContext();
        var bookingAfter = await dbAfter.Db.Bookings
            .FirstOrDefaultAsync(b => b.Id == expired.Id);
        bookingAfter!.Status.Should().Be(BookingStatusEnum.Expired);

        var eventAfter = await dbAfter.Db.Events
            .FirstOrDefaultAsync(e => e.Id == EventId.FromDatabase(eventId));
        var ticketTypeAfter = eventAfter!.TicketTypes.First();
        ticketTypeAfter.ReservedCount.Should().Be(0, "seats should be released after expiration");
    }

    [Fact]
    public async Task DeadLetterRequeue_WhenMessageMovedToDeadLetter_ShouldBeRequeueableAndProcessable()
    {
        var (eventId, ticketTypeId) = await SeedEventAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 1,
            PaymentMethod = 1,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var dbBefore = _fixture.CreateDbContext();
        var outboxMessage = await dbBefore.Db.OutboxMessages
            .FirstOrDefaultAsync(m => m.EventName == "BookingCreatedEvent");
        outboxMessage.Should().NotBeNull();

        var errorText = "Test: forced failure";
        var now = DateTime.UtcNow;

        using var scope = _fixture.Factory.Services.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        await outboxRepo.MoveToDeadLetterAsync(outboxMessage!.Id, errorText, now, CancellationToken.None);

        await using var dbDead = _fixture.CreateDbContext();
        var deadLetters = await dbDead.Db.OutboxDeadLetters.ToListAsync();
        deadLetters.Should().HaveCount(1);
        deadLetters[0].EventName.Should().Be("BookingCreatedEvent");

        await outboxRepo.RequeueDeadLetterAsync(deadLetters[0].Id, CancellationToken.None);

        await using var dbRequeue = _fixture.CreateDbContext();
        var requeuedMessage = await dbRequeue.Db.OutboxMessages
            .FirstOrDefaultAsync(m => m.EventName == "BookingCreatedEvent");
        requeuedMessage.Should().NotBeNull();
        requeuedMessage!.ProcessedOnUtc.Should().BeNull();

        var deadLettersAfter = await dbRequeue.Db.OutboxDeadLetters.ToListAsync();
        deadLettersAfter.Should().BeEmpty("dead letter should be removed after requeue");

        var driver = new Eventy.IntegrationTests.Helpers.OutboxTestDriver(_fixture.Factory.Services);
        var processed = await driver.ProcessOnceAsync();
        processed.Should().BeGreaterThan(0, "requeued message should be processable");
    }
}
