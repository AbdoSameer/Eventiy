using Application.Abstractions.Outbox;
using Domain.Aggregates.BookingAggregate.Events;
using Eventy.IntegrationTests.Assertions;
using Eventy.IntegrationTests.Fixtures;
using Eventy.IntegrationTests.Helpers;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Eventy.IntegrationTests.Features.Outbox;

[Collection("Integration")]
public class OutboxTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly HttpClient _client;

    public OutboxTests(IntegrationTestFixture fixture)
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
    public async Task AtomicCommitment_WhenBookingCreated_ShouldHaveUnprocessedOutboxMessage()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync();

        var response = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 2,
            PaymentMethod = 0,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var dbScope = _fixture.CreateDbContext();
        await dbScope.Db.ShouldHaveUnprocessedMessagesAsync("BookingCreatedEvent", 1);
        await dbScope.Db.ShouldHaveOutboxForEveryBookingAsync(1);
    }

    [Fact]
    public async Task AtomicCommitment_WithConcurrentBookings_ShouldProduceOutboxForEach()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync(
            eventCapacity: 100, ticketCapacity: 50, ticketPrice: 100m);

        int successCount = 0;
        for (int i = 0; i < 3; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/booking", new
            {
                EventId = eventId,
                TicketTypeId = ticketTypeId,
                Quantity = 1,
                PaymentMethod = 0,
            });
            if (response.StatusCode == HttpStatusCode.Created)
                successCount++;
        }

        await using var dbScope = _fixture.CreateDbContext();
        await dbScope.Db.ShouldHaveUnprocessedMessagesAsync("BookingCreatedEvent", successCount);
        await dbScope.Db.ShouldHaveOutboxForEveryBookingAsync(successCount);
    }

    [Fact]
    public async Task Diagnostic_CheckPayload()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync();

        var response = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 1,
            PaymentMethod = 0,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var dbScope = _fixture.CreateDbContext();
        var payload = await dbScope.Db.OutboxMessages
            .Where(m => m.EventName == "BookingCreatedEvent")
            .Select(m => m.Payload)
            .FirstAsync();

        payload.Should().NotBeNullOrWhiteSpace();

        using var scope = _fixture.Factory.Services.CreateScope();
        var serializer = scope.ServiceProvider.GetRequiredService<IEventSerializer>();

        var result = serializer.Deserialize("BookingCreatedEvent", payload);
        result.IsSuccess.Should().BeTrue($"deserialization failed. Payload[..200]: {payload[..Math.Min(payload.Length, 200)]}");

        var deserialized = result.Value as BookingCreatedEvent;
        deserialized.Should().NotBeNull("deserialized event should be a BookingCreatedEvent");
        deserialized!.BookingId.Should().NotBeNull("BookingId value object must be populated after deserialization");
        deserialized.UserId.Should().NotBeNull("UserId must be populated");
        deserialized.EventId.Should().NotBeNull("EventId must be populated");
    }

    [Fact]
    public async Task DisruptionScenario_WhenOutboxProcessed_ShouldMarkMessagesAsProcessed()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync();

        var response = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 1,
            PaymentMethod = 0,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await using (var preDbScope = _fixture.CreateDbContext())
        {
            await preDbScope.Db.ShouldHaveUnprocessedMessagesAsync("BookingCreatedEvent", 1);
        }

        var driver = new OutboxTestDriver(_fixture.Factory.Services);
        var processedCount = await driver.ProcessOnceAsync();
        processedCount.Should().Be(2, "two messages should be processed: BookingCreatedEvent + TicketTypeSeatsReservedEvent");

        await using (var postDbScope = _fixture.CreateDbContext())
        {
            await postDbScope.Db.ShouldHaveProcessedMessagesAsync("BookingCreatedEvent", 1);
            await postDbScope.Db.ShouldHaveZeroUnprocessedAsync("BookingCreatedEvent");
            await postDbScope.Db.ShouldHaveZeroDeadLettersAsync();
        }
    }

    [Fact]
    public async Task IdempotencyCheck_WhenSameMessageDispatchedTwice_ShouldOnlyProcessOnce()
    {
        var (eventId, ticketTypeId) = await _fixture.SeedPublishedEventAsync();

        // Use Deferred payment — no cascading events (BookingCreatedEventHandler skips Confirm)
        var response = await _client.PostAsJsonAsync("/api/booking", new
        {
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = 1,
            PaymentMethod = 1, // Deferred
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var driver = new OutboxTestDriver(_fixture.Factory.Services);

        var firstPass = await driver.ProcessOnceAsync();
        firstPass.Should().Be(2, "first dispatch processes BookingCreatedEvent + TicketTypeSeatsReservedEvent");

        await using (var dbScope = _fixture.CreateDbContext())
        {
            await dbScope.Db.ShouldHaveProcessedMessagesAsync("BookingCreatedEvent", 1);
        }

        var secondPass = await driver.ProcessOnceAsync();
        secondPass.Should().Be(0, "second dispatch finds no unprocessed messages — already processed and no cascading from Deferred payment");
    }
}
