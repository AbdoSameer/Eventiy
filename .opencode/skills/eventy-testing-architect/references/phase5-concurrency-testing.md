# Phase 5 — Concurrency & Race Condition Testing

The most critical testing layer for Eventy. Concurrency testing is **Business Correctness Testing under simultaneous execution**, not performance testing.

## Table of Contents
1. [Core Philosophy](#core-philosophy)
2. [Critical Race Scenarios](#critical-race-scenarios)
3. [Concurrent Execution Engine](#concurrent-execution-engine)
4. [Parallel Execution Strategies](#parallel-execution-strategies)
5. [Locking Strategy Testing](#locking-strategy-testing)
6. [Database Concurrency Validation](#database-concurrency-validation)
7. [Stress Testing](#stress-testing)
8. [Test Data Strategy](#test-data-strategy)
9. [Folder Structure](#folder-structure)
10. [Code Quality Rules](#code-quality-rules)

## Core Philosophy

The main question: **Can Eventy guarantee that when 1 ticket is available and 1000 users try to reserve, exactly 1 reservation succeeds?**

The invariant: `Successful Reservations <= Available Inventory` — always.

### Best Practice #1 — Test Invariants, Not Timing

Bad (timing-based, unreliable):
```csharp
Thread A starts
Thread.Sleep(50)  // timing-dependent
Thread B starts
```

Good (synchronized execution):
```csharp
Workers Ready → Release Signal → All Requests Start Together
```

Use `CountdownEvent`, `Barrier`, or `ManualResetEventSlim` for true synchronization.

## Critical Race Scenarios

### Scenario 1 — Last Ticket Race (Highest Priority)

```
Initial State:
    Event: Concert
    TicketType: Capacity = 1, Available = 1
    Users: User A, User B, User C, ... User 1000

Action: Reserve Ticket

Expected:
    Successful bookings = 1
    Failed bookings = 999
    Available tickets = 0
```

### Scenario 2 — Same User Double Booking

```
Problem: User clicks twice
    Request 1 → Reserve
    Request 2 → Reserve

Expected: Only one booking created

Protection: Unique constraint (UserId, TicketId) at database level
```

### Scenario 3 — Payment Callback Race

```
Problem: Payment provider sends PaymentCompleted event twice
Expected: One booking confirmation, not double-confirmed
```

### Scenario 4 — Limited Inventory

```
Initial State: 10 tickets available, 1000 users
Expected: Exactly 10 successful bookings
```

## Concurrent Execution Engine

Separates test orchestration from business logic.

Architecture:
```
Test Scenario
    ↓
ConcurrentExecutor
    ↓
N Tasks (synchronized via CountdownEvent)
    ↓
HTTP/MediatR Requests
    ↓
Collect Results
    ↓
Assert Database State
```

```csharp
public class ConcurrentExecutor
{
    public async Task<ConcurrentResult<T>> ExecuteAsync<T>(
        int workerCount,
        Func<Task<T>> action)
    {
        var countdown = new CountdownEvent(1); // Master signal
        var barrier = new CountdownEvent(workerCount); // Workers ready
        var results = new List<T>();
        var lockObj = new object();

        var tasks = Enumerable.Range(0, workerCount).Select(async _ =>
        {
            barrier.Signal();       // "I'm ready"
            countdown.Wait();       // Wait for master release

            var result = await action();

            lock (lockObj) results.Add(result);
        });

        barrier.Wait();             // Wait for all workers
        countdown.Signal();         // RELEASE ALL AT ONCE

        await Task.WhenAll(tasks);
        return new ConcurrentResult<T>(results);
    }
}
```

## Parallel Execution Strategies

| Strategy | Use Case | Count | Mechanism |
|----------|----------|-------|-----------|
| `Task.WhenAll` | Medium concurrency | 100 requests | Simple, async-friendly |
| `Parallel.ForEachAsync` | Large load | 1000+ requests | Controls parallelism, resource-efficient |
| `CountdownEvent` + `Barrier` | True race simulation | Any count | All workers release simultaneously |

### CountdownEvent Pattern (True Race)

```csharp
[Fact]
public async Task LastTicket_With100Users_ShouldAllowExactlyOneBooking()
{
    // Arrange
    var (event, ticket) = await _fixture.SeedSingleTicketEventAsync();
    var countdown = new CountdownEvent(1);
    var ready = new CountdownEvent(100);

    // Act: 100 users try to book simultaneously
    var tasks = Enumerable.Range(0, 100).Select(async userId =>
    {
        ready.Signal();
        countdown.Wait();

        return await _client.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(ticket.Id, Quantity: 1));
    });

    ready.Wait();           // All 100 workers are ready
    countdown.Signal();     // FIRE ALL AT ONCE

    var responses = await Task.WhenAll(tasks);

    // Assert HTTP results
    var successes = responses.Count(r => r.IsSuccessStatusCode);
    successes.Should().Be(1);

    // Assert database reality
    await using var db = _fixture.CreateDbContext();
    var bookingCount = await db.Bookings
        .CountAsync(b => b.TicketTypeId == ticket.Id);
    bookingCount.Should().Be(1);
}
```

### Parallel.ForEachAsync Pattern (Stress)

```csharp
[Fact]
public async Task TicketBurst_With1000Users_ShouldNotOverbook()
{
    var (event, ticket) = await _fixture.SeedLimitedTicketEventAsync(capacity: 10);
    var results = new ConcurrentBag<HttpResponseMessage>();

    await Parallel.ForEachAsync(
        Enumerable.Range(0, 1000),
        new ParallelOptions { MaxDegreeOfParallelism = 100 },
        async (userId, ct) =>
        {
            var response = await _client.PostAsJsonAsync("/api/bookings",
                new CreateBookingRequest(ticket.Id, Quantity: 1), ct);
            results.Add(response);
        });

    var successCount = results.Count(r => r.IsSuccessStatusCode);
    successCount.Should().BeLessThanOrEqualTo(10);

    await using var db = _fixture.CreateDbContext();
    var bookingCount = await db.Bookings
        .CountAsync(b => b.TicketTypeId == ticket.Id);
    bookingCount.Should().BeLessThanOrEqualTo(10);
}
```

## Locking Strategy Testing

### Option A — Optimistic Concurrency (EF Core RowVersion)

```csharp
// Entity
public class TicketType
{
    // ...
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;
}
```

Test expectation: One update succeeds, others get `DbUpdateConcurrencyException`.

```csharp
// Assert concurrency exception is handled gracefully
result.Error.Should().Be(TicketErrors.ConcurrentUpdate);
// NOT: catch(Exception) { /* silent */ }
```

### Option B — Pessimistic Locking (Database Row Lock)

SQL Server: `WITH (UPDLOCK, ROWLOCK, HOLDLOCK)`
PostgreSQL: `FOR UPDATE`

Test verifies: Only one transaction owns the lock at a time.

### Option C — Distributed Lock (Redis)

Used when Eventy runs on multiple servers:

```
Server A: Acquire Lock → Process Booking → Release Lock
Server B: Cannot Acquire → Wait or Fail
```

Test expectations:
- Lock acquired before booking
- Lock released after booking (success or failure)
- Lock expires with TTL if server crashes
- Another worker can continue after lock expiration

**Critical**: Distributed locks MUST have expiration (TTL). Never lock forever — if a server crashes, the lock must auto-expire.

## Database Concurrency Validation

Always validate TWO things:

1. **API Results**: Count success/failure responses
2. **Database Reality**: Query actual database state

Never trust only HTTP. The API could lie.

```csharp
// Assert both HTTP and database
var successCount = responses.Count(r => r.IsSuccessStatusCode);
successCount.Should().Be(1);

await using var db = _fixture.CreateDbContext();
var bookingCount = await db.Bookings.CountAsync();
bookingCount.Should().Be(1);  // Independent verification
```

## Stress Testing

After correctness tests pass, increase load:

| Level | Users | Purpose |
|-------|-------|---------|
| Level 1 | 100 | Basic validation |
| Level 2 | 1,000 | Database contention |
| Level 3 | 10,000 | System limits |

Measure: Response time, database errors, deadlocks, memory usage, CPU, successful bookings.

**Best Practice**: Separate stress tests from CI. Run on nightly pipeline, not every commit.

## Test Data Strategy

Create dedicated concurrency scenarios:

```csharp
public static class ConcurrencyScenarios
{
    // Single ticket, many users — the classic race
    public static async Task<(Event, TicketType)> SingleTicketAsync(this IntegrationTestFixture f)
    {
        return await f.SeedEventWithTicketsAsync(capacity: 1);
    }

    // Limited inventory — exactly N should succeed
    public static async Task<(Event, TicketType)> LimitedInventoryAsync(
        this IntegrationTestFixture f, int capacity = 10)
    {
        return await f.SeedEventWithTicketsAsync(capacity);
    }

    // Multiple events — check isolation (booking one shouldn't affect another)
    public static async Task<List<(Event, TicketType)>> MultipleEventsAsync(
        this IntegrationTestFixture f, int eventCount = 10)
    {
        var results = new List<(Event, TicketType)>();
        for (int i = 0; i < eventCount; i++)
            results.Add(await f.SeedEventWithTicketsAsync(capacity: 10));
        return results;
    }
}
```

## Folder Structure

```
Eventy.ConcurrencyTests/
├── Scenarios/
│   ├── LastTicketRaceTests.cs
│   ├── DuplicateBookingTests.cs
│   ├── PaymentCallbackRaceTests.cs
│   └── OversellingPreventionTests.cs
├── Engine/
│   ├── ConcurrentExecutor.cs
│   ├── BarrierCoordinator.cs
│   └── StressRunner.cs
├── Assertions/
│   └── ConcurrencyAssertions.cs
└── Fixtures/
    └── ConcurrencyTestFixture.cs
```

## Code Quality Rules

| Rule | Guideline |
|------|-----------|
| Deterministic | No random users, no random delays, no `Thread.Sleep()` |
| Meaningful names | `ReserveLastTicket_ShouldPreventOverselling()` not `StressTest1()` |
| Independent | Each test creates own Event, own Tickets, own Users |
| Diagnostics on failure | Record request count, successful bookings, database state, exception list |
| Assert DB state | HTTP + Database + Inventory count — never HTTP alone |
| Synchronize properly | Use CountdownEvent/Barrier, never timing-based coordination |
| Separate from CI | Stress tests run nightly, not on every commit |

## Completion Criteria

- Last ticket race scenario covered
- Overselling proven impossible
- Database locking behavior verified
- Concurrency exceptions handled explicitly
- Distributed locks tested (if used)
- Stress scenarios documented
- Tests produce diagnostics on failure
