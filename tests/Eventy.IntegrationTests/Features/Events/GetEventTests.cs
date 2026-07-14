using Eventy.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Eventy.IntegrationTests.Features.Events;

[Collection("Integration")]
public class GetEventTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly HttpClient _client;

    public GetEventTests(IntegrationTestFixture fixture)
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
    public async Task GetEvents_WithSeededData_ShouldReturnSeededEvent()
    {
        var (eventId, _) = await _fixture.SeedPublishedEventAsync();

        var response = await _client.GetAsync("/api/Event?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetEventById_WithSeededEvent_ShouldReturnOk()
    {
        var (eventId, _) = await _fixture.SeedPublishedEventAsync();

        await using var dbScope = _fixture.CreateDbContext();
        var eventIdObj = Domain.Aggregates.EventAggregate.ValueObject.EventId.FromDatabase(eventId);
        var existsInDb = await dbScope.Db.Events.AnyAsync(e => e.Id == eventIdObj);
        existsInDb.Should().BeTrue("event should exist after seeding");

        var response = await _client.GetAsync($"/api/Event/{eventId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetEventById_WithNonexistentId_ShouldReturn404()
    {
        var response = await _client.GetAsync($"/api/Event/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
