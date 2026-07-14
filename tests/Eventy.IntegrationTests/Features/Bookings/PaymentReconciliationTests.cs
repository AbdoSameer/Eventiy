using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Payments;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Eventy.IntegrationTests.Fixtures;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using EventId = Domain.Aggregates.EventAggregate.ValueObject.EventId;

namespace Eventy.IntegrationTests.Features.Bookings;

/// <summary>
/// Verifies that the PaymentReconciliationJob correctly identifies and cancels
/// orphaned Instant bookings past their hold expiry.
/// </summary>
[Collection("Integration")]
public class PaymentReconciliationTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly HttpClient _client;

    public PaymentReconciliationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    public async Task InitializeAsync() => await _fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PaymentReconciliation_WhenOrphanedInstantBookingPastHold_ShouldCancelPayment()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 2,
            PaymentMethod = 0,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var dbBefore = _fixture.CreateDbContext();
        var booking = await dbBefore.Db.Bookings
            .FirstOrDefaultAsync(b => b.EventId == EventId.FromDatabase(eventId));
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatusEnum.Pending);
        booking.PaymentMethod.Should().Be(PaymentMethod.Instant);

        var holdExpiresAt = booking.HoldExpiresAt!.Value;
        var now = holdExpiresAt.AddSeconds(1);

        using var scope = _fixture.Factory.Services.CreateScope();
        var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var paymentService = scope.ServiceProvider.GetRequiredService<Application.Abstractions.Payments.IPaymentService>();

        var orphanedBookings = await bookingRepo.GetPendingInstantBookingsPastHoldAsync(
            now, 50, CancellationToken.None);

        orphanedBookings.Should().HaveCount(1);
        orphanedBookings[0].Id.Should().Be(booking.Id);

        var cancelResult = await paymentService.CancelPaymentAsync(
            orphanedBookings[0].Id.Value, CancellationToken.None);

        cancelResult.IsSuccess.Should().BeTrue(
            "payment service should successfully cancel the orphaned payment session");
    }
}
