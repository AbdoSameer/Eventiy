using Domain.Aggregates.EventAggregate;
using Domain.Aggregates.EventAggregate.Enums;
using Domain.Aggregates.EventAggregate.ValueObject;
using Domain.Primitives;
using Eventy.Testing.Foundation.Web;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Eventy.ConcurrencyTests.Fixtures;

/// <summary>
/// Shared fixture for concurrency tests. Same container/factory pattern as integration
/// but with helpers for concurrent scenario setup.
/// </summary>
public class ConcurrencyTestFixture : IAsyncLifetime
{
    public EventyWebApplicationFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Factory = new EventyWebApplicationFactory();
        await Factory.InitializeAsync();
        Client = Factory.CreateClient();
    }

    /// <summary>
    /// Creates a scoped DbContext. The returned wrapper disposes both scope and context
    /// on DisposeAsync, preventing resource leaks in long-lived test fixtures.
    /// </summary>
    public ScopedDbContext CreateDbContext()
    {
        var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new ScopedDbContext(db, scope);
    }

    /// <summary>
    /// Seeds a published event with a single ticket type into the test database.
    /// </summary>
    public async Task<(Guid EventId, Guid TicketTypeId)> SeedPublishedEventAsync(int capacity = 1, decimal price = 100m)
    {
        var utcNow = DateTime.UtcNow;
        var address = Address.Create("Egypt", "Cairo", "Test Street", "11511", 30.0444, 31.2357).Value;

        var eventResult = Event.Create(
            "Concurrency Test Event", capacity, utcNow.AddDays(30),
            address, "Seeded for concurrency test", EventType.Tech, utcNow);

        if (eventResult.IsFailure)
            throw new InvalidOperationException($"Failed to create seed event: {eventResult.Errors[0].Message}");

        var @event = eventResult.Value;
        @event.Publish(utcNow);

        var money = Money.FromDecimal(price, "EGP");
        if (money.IsFailure)
            throw new InvalidOperationException($"Failed to create money: {money.Errors[0].Message}");

        @event.AddTicketType("General", money.Value, capacity, utcNow);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Events.Add(@event);
        await db.SaveChangesAsync();

        var ticketTypeId = @event.TicketTypes.First().Id;
        return (@event.Id.Value, ticketTypeId.Value);
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();
}

/// <summary>
/// Wraps an ApplicationDbContext with its creation scope so both are disposed together.
/// Usage: <c>await using var db = fixture.CreateDbContext();</c>
/// </summary>
public sealed class ScopedDbContext : IAsyncDisposable
{
    public ApplicationDbContext DbContext { get; }
    private readonly IServiceScope _scope;

    public ScopedDbContext(ApplicationDbContext dbContext, IServiceScope scope)
    {
        DbContext = dbContext;
        _scope = scope;
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        _scope.Dispose();
    }
}

[CollectionDefinition("Concurrency")]
public class ConcurrencyCollection : ICollectionFixture<ConcurrencyTestFixture> { }
