using Application.Abstractions.Payments;
using Domain.Abstractions.Persistence;
using Domain.Aggregates.BookingAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Eventy.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using Xunit.Abstractions;
using EventId = Domain.Aggregates.EventAggregate.ValueObject.EventId;

namespace Eventy.IntegrationTests.Features.Bookings;

/// <summary>
/// Verifies that the PaymentReconciliationJob correctly identifies and
/// cancels orphaned Instant bookings past their hold expiry.
///
/// Migrated to IntegrationTestBase for consistent DB reset + state logging.
/// </summary>
public class PaymentReconciliationTests : IntegrationTestBase
{
    public PaymentReconciliationTests(IntegrationTestFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    [Fact]
    public async Task PaymentReconciliation_WhenOrphanedInstantBookingPastHold_ShouldCancelPayment()
    {
        var (eventId, ticketTypeId) = await Data.CreatePublishedEventAsync();

        var createResponse = await Client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 2,
            PaymentMethod = 0,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await State.LogAsync("After booking (Instant payment)", eventId, ticketTypeId);

        await using var dbBefore = Fixture.CreateDbContext();
        var booking = await dbBefore.Db.Bookings
            .FirstOrDefaultAsync(b => b.EventId == EventId.FromDatabase(eventId));
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatusEnum.Pending);
        booking.PaymentMethod.Should().Be(PaymentMethod.Instant);

        var holdExpiresAt = booking.HoldExpiresAt!.Value;
        var now = holdExpiresAt.AddSeconds(1);

        using var scope = Fixture.Factory.Services.CreateScope();
        var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

        var orphanedBookings = await bookingRepo.GetPendingInstantBookingsPastHoldAsync(
            now, 50, CancellationToken.None);

        orphanedBookings.Should().HaveCount(1);
        orphanedBookings[0].Id.Should().Be(booking.Id);

        var cancelResult = await paymentService.CancelPaymentAsync(
            orphanedBookings[0].Id.Value, CancellationToken.None);

        cancelResult.IsSuccess.Should().BeTrue(
            "payment service should successfully cancel the orphaned payment session");

        await State.LogAsync("After payment cancellation", eventId, ticketTypeId);
    }
}
