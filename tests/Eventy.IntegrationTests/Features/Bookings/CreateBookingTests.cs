using Eventy.IntegrationTests.Fixtures;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Eventy.IntegrationTests.Features.Bookings;

[Collection("Integration")]
public class CreateBookingTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly HttpClient _client;

    public CreateBookingTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateBooking_WhenValidRequest_ShouldReturn201WithLocation()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync(
            eventCapacity: 100, ticketCapacity: 50, ticketPrice: 100m);

        var response = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 2,
            PaymentMethod = 0, // Instant
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.AbsolutePath.Should().Match("/api/Booking/*");

        await using var dbScope = _fixture.CreateDbContext();
        var eventIdObj = Domain.Aggregates.EventAggregate.ValueObject.EventId.FromDatabase(eventId);
        var bookingCount = await dbScope.Db.Bookings
            .CountAsync(b => b.EventId == eventIdObj);
        bookingCount.Should().Be(1, "exactly one booking should be created for the seeded event");
    }

    [Fact]
    public async Task CreateBooking_WhenNonexistentEvent_ShouldReturn404()
    {
        var response = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Quantity = 1,
            PaymentMethod = 0,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
