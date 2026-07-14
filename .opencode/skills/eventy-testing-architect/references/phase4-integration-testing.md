# Phase 4 — Integration Testing Architecture

Integration tests verify the complete production flow: HTTP → Controller → MediatR Pipeline → Command Handler → Domain Logic → EF Core → Real Database → Outbox/Event Storage.

## Table of Contents
1. [Core Principle](#core-principle)
2. [Integration Test Categories](#integration-test-categories)
3. [Database Strategy](#database-strategy)
4. [Testcontainers Lifecycle](#testcontainers-lifecycle)
5. [WebApplicationFactory Setup](#webapplicationfactory-setup)
6. [Authentication Testing](#authentication-testing)
7. [Test Data Architecture](#test-data-architecture)
8. [Outbox Pattern Testing](#outbox-pattern-testing)
9. [Folder Structure](#folder-structure)
10. [Quality Rules](#quality-rules)

## Core Principle

Integration Tests test **real system behavior**, not mocked behavior.

Bad approach (do not do this):
```
Controller Test → Mock Handler → Mock Repository → Return Fake Result
```

This only tests: Controller + Mock Setup, not the actual system.

Good approach (vertical slice testing):
```
HTTP Request → API Controller → MediatR Pipeline → Command Handler → EF Core → Real Database
```

Prefer feature-based test organization over layer-based:
- Good: `CreateBookingTests.cs`, `CancelBookingTests.cs` (feature scenarios)
- Bad: `ControllerTests.cs`, `HandlerTests.cs`, `RepositoryTests.cs` (layer tests)

## Integration Test Categories

### Category 1 — API Endpoint Tests

Verify HTTP behavior including request model binding, validation, authorization, response status codes, and database persistence:

```csharp
[Fact]
public async Task CreateEvent_WithValidRequest_ShouldReturn201AndPersist()
{
    // Arrange
    var request = new CreateEventRequest("Concert", 100, DateTime.UtcNow.AddDays(30));

    // Act
    var response = await _client.PostAsJsonAsync("/api/events", request);

    // Assert HTTP
    response.StatusCode.Should().Be(HttpStatusCode.Created);

    // Assert Database
    await using var db = _fixture.CreateDbContext();
    var persistedEvent = await db.Events.FirstAsync();
    persistedEvent.Name.Should().Be("Concert");
    persistedEvent.Capacity.Should().Be(100);
}
```

### Category 2 — Application Pipeline Tests

Verify MediatR pipeline behaviors execute in the correct order:

```csharp
Request → Validation Behavior → Authorization Behavior → Transaction Behavior → Handler
```

A unit test of the Handler does NOT prove pipeline registration works.

### Category 3 — Persistence Tests

Verify real database behavior: relationships, foreign keys, cascading rules, constraints, transactions.

```csharp
[Fact]
public async Task CreateBooking_ShouldSaveBookingAndOutboxAtomically()
{
    // Arrange: seed event with available tickets
    var (event, ticket) = await _fixture.SeedAvailableTicketEventAsync();

    // Act
    var response = await _client.PostAsJsonAsync("/api/bookings",
        new CreateBookingRequest(ticket.Id, Quantity: 2));

    // Assert: both Booking and OutboxMessage exist
    await using var db = _fixture.CreateDbContext();
    var booking = await db.Bookings.FirstAsync();
    var outboxMessage = await db.OutboxMessages.FirstAsync();

    booking.Should().NotBeNull();
    outboxMessage.Should().NotBeNull();
}
```

### Category 4 — Outbox Pattern Tests

The Outbox guarantees business data + domain event message are stored together atomically.

Test scenario:
```
Action: Reserve Ticket
Expected:
    Database: Bookings Table + OutboxMessages Table — both must exist
```

See [Outbox Pattern Testing](#outbox-pattern-testing) below for details.

## Database Strategy

### Why Testcontainers + Real SQL Server (Not EF InMemory)

| Capability | EF InMemory | Real SQL Server |
|-----------|-------------|-----------------|
| Transactions | Object collection update | BEGIN/COMMIT/ROLLBACK |
| Constraints | None | Foreign keys, unique indexes |
| Locking | None | Row locks, isolation levels, deadlocks |
| Query behavior | LINQ on objects | SELECT FOR UPDATE, execution plans |
| Concurrency tokens | Ignored | RowVersion properly validated |

For ticketing systems: **EF InMemory = unacceptable**.

### Container Lifecycle

```
Test Started
    → Create SQL Server Container
    → Create Empty Database
    → Run EF Core Migrations
    → Seed Required Data
    → Execute Test
    → Reset Database (Respawn — between tests)
    → Execute Next Test
    → ...
    → Destroy Container (end of collection)
```

Use Respawn for database reset between tests, not container recreation. Creating a container per test is too slow.

### IntegrationTestFixture

```csharp
public class IntegrationTestFixture : IAsyncLifetime
{
    public EventyWebApplicationFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Factory = new EventyWebApplicationFactory();
        Client = Factory.CreateClient();
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();

    public ApplicationDbContext CreateDbContext()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationTestFixture> { }
```

## WebApplicationFactory Setup

See [Phase 2 — Testing Foundation](phase2-testing-foundation.md) for the full WebApplicationFactory implementation. Key overrides for integration tests:

```csharp
builder.ConfigureServices(services =>
{
    // 1. Replace DbContext with test container connection
    // 2. Replace Redis with FakeCacheService
    // 3. Replace external services (payment, email) with fakes
    // 4. Disable background workers (OutboxProcessor, BookingExpirationJob)
    // 5. Add test authentication scheme
});
```

## Authentication Testing

Different users for different scenarios:

```csharp
public static class TestUsers
{
    public static ClaimsPrincipal UserA =>
        CreateUser("11111111-1111-1111-1111-111111111111", "User A");

    public static ClaimsPrincipal UserB =>
        CreateUser("22222222-2222-2222-2222-222222222222", "User B");

    // Concurrency tests need different users competing for same resource
}
```

Override `TestAuthenticationHandler` to return different identities based on test scenario:

```csharp
// In test, set the desired user before the request
_factory.WithUser(TestUsers.UserA);
var response = await _client.PostAsync("/api/bookings", content);
```

## Test Data Architecture

Use builder pattern + scenario helpers:

```csharp
// Bad: raw object creation
events.Add(new Event { Name = "Test", Capacity = 100 });

// Good: fluent builder
var event = new EventBuilder()
    .WithName("Rock Concert")
    .WithCapacity(500)
    .WithDate(DateTime.UtcNow.AddDays(30))
    .Build();

// Better: scenario-level builder for common situations
var (event, ticket) = await _fixture.SeedSoldOutEventAsync();
var (event2, ticket2) = await _fixture.SeedExpiredEventAsync();
```

## Outbox Pattern Testing

Critical for Eventy: verify business transaction + event publishing are atomic.

```csharp
[Fact]
public async Task ReserveTicket_ShouldCreateBookingAndOutboxMessage()
{
    // Arrange
    var (event, ticket) = await _fixture.SeedAvailableTicketEventAsync();

    // Act
    var response = await _client.PostAsJsonAsync("/api/bookings",
        new CreateBookingRequest(ticket.Id, Quantity: 1));

    // Assert
    response.EnsureSuccessStatusCode();

    await using var db = _fixture.CreateDbContext();

    // Verify booking persisted
    var booking = await db.Bookings
        .FirstOrDefaultAsync(b => b.TicketTypeId == ticket.Id);
    booking.Should().NotBeNull();

    // Verify outbox message created with correct event data
    var outboxMessage = await db.OutboxMessages
        .FirstOrDefaultAsync(o => o.Type == typeof(BookingCreatedDomainEvent).FullName);
    outboxMessage.Should().NotBeNull();
    outboxMessage.Payload.Should().Contain(booking.Id.ToString());
}
```

## Folder Structure

```
Eventy.IntegrationTests/
├── Features/
│   ├── Events/
│   │   └── CreateEventTests.cs
│   ├── Tickets/
│   │   └── ReserveTicketTests.cs
│   └── Bookings/
│       ├── CreateBookingTests.cs
│       ├── CancelBookingTests.cs
│       └── ConfirmBookingTests.cs
├── Fixtures/
│   ├── WebApplicationFixture.cs
│   └── DatabaseFixture.cs
├── Scenarios/
│   ├── AvailableTicketScenario.cs
│   ├── SoldOutEventScenario.cs
│   └── ExpiredBookingScenario.cs
├── Assertions/
│   └── DatabaseAssertions.cs
└── Helpers/
    └── HttpResponseExtensions.cs
```

## Quality Rules

| Rule | Guideline |
|------|-----------|
| Test user behavior | Assert "Booking exists in database" not "Repository method called" |
| One scenario per test | Separate `CreateBooking` and `CancelBooking` tests |
| Keep tests independent | Each test arranges its own data and database state |
| Ratio | Unit 70% : Integration 25% : Concurrency 5% |
| Assert database state | Never trust only HTTP responses — verify persisted data |
| Fast execution target | Integration suite should complete in under 5 minutes |

## Completion Criteria

- Real SQL Server testing environment exists via Testcontainers
- API runs inside test host (WebApplicationFactory)
- EF migrations execute automatically
- Commands execute through full MediatR pipeline
- Database state verifiable after each test
- Outbox behavior tested atomically
- Authentication scenarios covered
- Tests run isolated in CI/CD
