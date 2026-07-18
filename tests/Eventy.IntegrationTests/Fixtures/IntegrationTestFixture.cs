using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Primitives;
using Eventy.Testing.Foundation.Database;
using Eventy.Testing.Foundation.Web;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Eventy.IntegrationTests.Fixtures;

/// <summary>
/// Shared fixture for all integration tests. Spins up ONE SQL Server container
/// and ONE WebApplicationFactory for the entire test collection.
/// Implements IAsyncLifetime so per-test classes can chain cleanup via IAsyncLifetime.
/// </summary>
public class IntegrationTestFixture : IAsyncLifetime
{
    public EventyWebApplicationFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    private DatabaseResetService? _dbReset;

    public async Task InitializeAsync()
    {
        Factory = new EventyWebApplicationFactory();
        await Factory.InitializeAsync();
        Client = Factory.CreateClient();

        _dbReset = new DatabaseResetService(Factory.ConnectionString);

        await _dbReset.InitializeAsync();
    }

    /// <summary>
    /// Resets all tables (preserves schema) between test classes.
    /// Call from test constructor or IAsyncLifetime.InitializeAsync for per-test isolation.
    /// </summary>
    public Task ResetDatabaseAsync() => _dbReset!.ResetAsync();

    /// <summary>
    /// Seeds a published event with a single "General" ticket type into the test database.
    /// Returns the EventId and TicketTypeId for use in booking requests.
    /// </summary>
    public async Task<(Guid EventId, Guid TicketTypeId)> SeedPublishedEventAsync(
        int eventCapacity = 100,
        int ticketCapacity = 50,
        decimal ticketPrice = 100m)
    {
        var utcNow = DateTime.UtcNow;
        var address = Address.Create("Egypt", "Cairo", "Test Street", "11511", 30.0444, 31.2357).Value;

        var eventResult = Event.Create(
            "Integration Test Event", eventCapacity, utcNow.AddDays(30),
            address, "Seeded by integration test", EventType.Tech, utcNow);

        Domain.Aggregates.EventAggregate.Event @event;
        if (eventResult.IsFailure)
            throw new InvalidOperationException($"Failed to create seed event: {eventResult.Errors[0].Message}");

        @event = eventResult.Value;

        var money = Money.FromDecimal(ticketPrice, "EGP");
        if (money.IsFailure)
            throw new InvalidOperationException($"Failed to create money: {money.Errors[0].Message}");

        @event.AddTicketType("General", money.Value, ticketCapacity, utcNow);
        @event.Publish(utcNow);

        // Persist via scoped DbContext (not Factory.Services scope — that's the app's scope)
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Events.Add(@event);
        await db.SaveChangesAsync();

        var ticketTypeId = @event.TicketTypes.First().Id;
        return (@event.Id.Value, ticketTypeId.Value);
    }

    /// <summary>
    /// Creates a scoped ApplicationDbContext for direct DB assertions.
    /// </summary>
    public ScopedIntegrationDbContext CreateDbContext()
    {
        var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new ScopedIntegrationDbContext(db, scope);
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();
}

/// <summary>
/// Wraps an ApplicationDbContext with its creation scope so both are disposed together.
/// </summary>
public sealed class ScopedIntegrationDbContext : IAsyncDisposable
{
    public ApplicationDbContext Db { get; }
    private readonly IServiceScope _scope;

    public ScopedIntegrationDbContext(ApplicationDbContext db, IServiceScope scope)
    {
        Db = db;
        _scope = scope;
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        _scope.Dispose();
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationTestFixture> { }
