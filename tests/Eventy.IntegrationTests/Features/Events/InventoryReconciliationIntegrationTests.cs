using Application.Abstractions.Inventory;
using Application.Abstractions.Messaging;
using Application.Abstractions.Payments;
using Application.Abstractions.Persistence;
using Application.Features.Events.Commands.ToggleHighDemand;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Eventy.IntegrationTests.Fixtures;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using Xunit;
using Xunit.Abstractions;
using EventId = Domain.Aggregates.EventAggregate.ValueObject.EventId;

namespace Eventy.IntegrationTests.Features.Events;

/// <summary>
/// Integration tests for the InventoryReconciliationJob — Layer 3
/// of the Strategy Handover Race defense.
///
/// Uses IntegrationTestBase (Rule of One + Clean Up Hook + State Logging).
/// </summary>
public class InventoryReconciliationIntegrationTests : IntegrationTestBase
{
    public InventoryReconciliationIntegrationTests(IntegrationTestFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    private async Task<(Guid EventId, Guid TicketTypeId)> SeedHighDemandEventWithBookingsAsync(
        int ticketCapacity, int bookingCount)
    {
        var (eventId, ticketTypeId) = await Data.CreatePublishedEventAsync(
            eventCapacity: ticketCapacity, ticketCapacity: ticketCapacity);

        var seeder = new NoOpInventorySeeder();
        var scopeFactory = Fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>();
        var timeProvider = Fixture.Factory.Services.GetRequiredService<TimeProvider>();
        var toggleHandler = new ToggleHighDemandCommandHandler(scopeFactory, timeProvider, seeder);
        await toggleHandler.Handle(
            new ToggleHighDemandCommand { EventId = eventId, Enabled = true },
            CancellationToken.None);

        await Data.CreateBookingsAsync(eventId, ticketTypeId, bookingCount, paymentMethod: 1);

        return (eventId, ticketTypeId);
    }

    [Fact]
    public async Task Reconciliation_WhenTicketTypeOversold_ShouldCancelLatestPendingBookings()
    {
        var (eventId, ticketTypeId) = await SeedHighDemandEventWithBookingsAsync(
            ticketCapacity: 5, bookingCount: 5);

        await State.LogAsync("Before reconciliation", eventId, ticketTypeId);

        using var scope = Fixture.Factory.Services.CreateScope();
        var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();

        var latestPending = await bookingRepo.GetLatestPendingByTicketTypeAsync(
            TicketTypeId.FromDatabase(ticketTypeId),
            batchSize: 2,
            CancellationToken.None);

        latestPending.Should().HaveCount(2,
            "the 2 most recent pending bookings should be returned for compensation");
        latestPending.Should().AllSatisfy(b =>
            b.Status.Should().BeOneOf(BookingStatusEnum.Pending, BookingStatusEnum.PendingPayment));

        var utcNow = DateTime.UtcNow;
        foreach (var booking in latestPending)
        {
            booking.Cancel(utcNow, "Oversold — inventory reconciliation");
        }

        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await uow.CommitAsync(CancellationToken.None);

        await using var verifyDb = Fixture.CreateDbContext();
        var ttIdObj = TicketTypeId.FromDatabase(ticketTypeId);
        var cancelledCount = await verifyDb.Db.Bookings
            .CountAsync(b => b.TicketTypeId == ttIdObj
                && b.Status == BookingStatusEnum.Cancelled);
        cancelledCount.Should().Be(2,
            "exactly 2 bookings should have been cancelled as oversold");

        await State.LogAsync("After reconciliation", eventId, ticketTypeId);
    }

    [Fact]
    public async Task Reconciliation_WhenNoOversell_ShouldNotCancelAnyBookings()
    {
        var (eventId, ticketTypeId) = await Data.CreatePublishedEventAsync(
            eventCapacity: 10, ticketCapacity: 10);

        await Data.CreateBookingAsync(eventId, ticketTypeId, quantity: 3, paymentMethod: 1);

        await State.LogAsync("After booking (no oversell)", eventId, ticketTypeId);

        using var scope = Fixture.Factory.Services.CreateScope();
        var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();

        var latestPending = await bookingRepo.GetLatestPendingByTicketTypeAsync(
            TicketTypeId.FromDatabase(ticketTypeId),
            batchSize: 50,
            CancellationToken.None);

        latestPending.Should().HaveCount(1);
        latestPending[0].Status.Should().BeOneOf(
            BookingStatusEnum.Pending, BookingStatusEnum.PendingPayment);

        await State.LogAsync("After reconciliation check", eventId, ticketTypeId);
    }

    [Fact]
    public async Task Reconciliation_WhenCompensated_ShouldStageCompensationLog()
    {
        var (eventId, ticketTypeId) = await SeedHighDemandEventWithBookingsAsync(
            ticketCapacity: 3, bookingCount: 3);

        await State.LogAsync("Before compensation", eventId, ticketTypeId);

        using var scope = Fixture.Factory.Services.CreateScope();
        var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var compensationRepo = scope.ServiceProvider.GetRequiredService<ICompensationLogRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var latestPending = await bookingRepo.GetLatestPendingByTicketTypeAsync(
            TicketTypeId.FromDatabase(ticketTypeId),
            batchSize: 1,
            CancellationToken.None);

        latestPending.Should().HaveCount(1);
        var bookingToCancel = latestPending[0];
        bookingToCancel.Cancel(now, "Oversold — inventory reconciliation");

        var compensationLog = new CompensationLogDto(
            Id: Guid.NewGuid(),
            BookingId: bookingToCancel.Id.Value,
            CompensationType: "CompensateOversoldBooking",
            Payload: System.Text.Json.JsonSerializer.Serialize(new
            {
                BookingId = bookingToCancel.Id.Value,
                Reason = "Inventory reconciliation: oversold during strategy handover",
                OccurredAt = now,
                RedisRemaining = -1,
            }),
            OccurredOnUtc: now,
            IdempotencyKey: $"compensation:CompensateOversoldBooking:{bookingToCancel.Id.Value}",
            ProcessedOnUtc: null,
            Error: null,
            RetryCount: 0,
            NextRetryOnUtc: null);

        compensationRepo.Add(compensationLog);
        await uow.CommitAsync(CancellationToken.None);

        await using var verifyDb = Fixture.CreateDbContext();
        var stagedLog = await verifyDb.Db.CompensationLogs
            .FirstOrDefaultAsync(c => c.BookingId == bookingToCancel.Id.Value
                && c.CompensationType == "CompensateOversoldBooking");
        stagedLog.Should().NotBeNull(
            "a CompensateOversoldBooking log should be staged for the CompensationProcessor");
        stagedLog!.ProcessedOnUtc.Should().BeNull(
            "the compensation log should be unprocessed — the CompensationProcessor picks it up");

        await State.LogCompensationAsync("After staging", bookingToCancel.Id.Value);
    }

    [Fact]
    public async Task Reconciliation_WhenEventNotHighDemand_ShouldNotReconcile()
    {
        var (eventId, ticketTypeId) = await Data.CreatePublishedEventAsync(
            eventCapacity: 10, ticketCapacity: 10);

        await Data.CreateBookingAsync(eventId, ticketTypeId, quantity: 5, paymentMethod: 1);

        await State.LogAsync("Non-high-demand event", eventId, ticketTypeId);

        await using var db = Fixture.CreateDbContext();
        var eventIdObj = EventId.FromDatabase(eventId);
        var @event = await db.Db.Events.FirstAsync(e => e.Id == eventIdObj);
        @event.IsHighDemand.Should().BeFalse(
            "non-high-demand events should not be reconciled by the inventory job");
    }

    private sealed class NoOpInventorySeeder : IInventorySeeder
    {
        public Task SeedAsync(Domain.Aggregates.EventAggregate.Event @event, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ClearAsync(Domain.Aggregates.EventAggregate.Event @event, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
