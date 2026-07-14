---
name: eventy-testing-architect
description: Comprehensive testing architecture skill for DDD/CQRS/event-driven ticketing systems (like Eventy) using .NET, EF Core, MediatR, and Testcontainers. Use when generating, designing, or reviewing tests for domain aggregates, command handlers, API endpoints, database concurrency, or race condition scenarios. Covers 3-tier testing — Unit (xUnit/NSubstitute/FluentAssertions), Integration (WebApplicationFactory/Testcontainers/Respawn), and Concurrency (barrier-synchronized parallel execution) — plus CI/CD pipeline design. Trigger on requests involving test strategy, test scaffolding, DDD unit tests, integration test infrastructure, concurrency/race-condition testing, outbox pattern testing, or ticketing system quality assurance.
---

# Eventy Testing Architect

Master testing architecture for DDD/CQRS event ticketing systems. Guides generation of production-grade tests across 3 tiers: Unit, Integration, and Concurrency.

## Core Philosophy

> Unit Tests protect business decisions. Integration Tests verify system communication. Concurrency Tests prevent inventory corruption.

Testing priority: **Domain Rules > Application Flow > Infrastructure Details**

The most dangerous bug class in a ticketing platform:

```
Two valid requests + Same resource + Same millisecond = Inventory corruption
```

Therefore concurrency testing is a first-class testing layer, not an optional performance test.

## 3-Tier Testing Architecture

```
Tier A — Unit Tests          Tier B — Integration Tests       Tier C — Concurrency Tests
Fast (< 1s)                  Slower (seconds)                 Controlled chaos
No DB / No HTTP              Real DB + Real pipeline          Synchronized parallel
Domain + Application         Full vertical slice              Race condition detection
```

### Tier A — Unit Testing
- **Target**: Domain Aggregates, Value Objects, Domain Events, Command Handlers, Pipeline Behaviors
- **Tools**: xUnit + NSubstitute + FluentAssertions
- **Principles**: Fast, deterministic, no database, no HTTP, no Docker
- **Pattern**: AAA (Arrange-Act-Assert) / Given-When-Then
- **Naming**: `Method_WhenCondition_ShouldExpectedResult`
- **See**: [references/phase3-unit-testing.md](references/phase3-unit-testing.md) for full domain and application unit testing architecture

### Tier B — Integration Testing
- **Target**: Full vertical slice (HTTP -> Controller -> MediatR -> Handler -> EF Core -> Database -> Outbox)
- **Tools**: xUnit + Microsoft.AspNetCore.Mvc.Testing + Testcontainers + Respawn + FluentAssertions
- **Database**: Real SQL Server/PostgreSQL via Testcontainers — **never EF Core InMemory**
- **See**: [references/phase4-integration-testing.md](references/phase4-integration-testing.md) for WebApplicationFactory, database lifecycle, and outbox testing

### Tier C — Concurrency Testing
- **Target**: Race conditions, inventory corruption, distributed locking
- **Core scenario**: 1000 users, 1 ticket, exactly 1 success
- **Algorithms**: Task.WhenAll, CountdownEvent, Parallel.ForEachAsync
- **See**: [references/phase5-concurrency-testing.md](references/phase5-concurrency-testing.md) for execution engine, locking validation, and stress testing

## Testing Foundation

Before writing tests, establish the shared infrastructure:

```
tests/
├── Eventy.Testing.Foundation/      <- Shared infrastructure library
│   ├── Containers/
│   ├── Web/                        <- WebApplicationFactory
│   ├── Database/                   <- Migration runner, reset service
│   ├── Fixtures/
│   └── Builders/                   <- EventBuilder, TicketBuilder, BookingBuilder
├── Eventy.Domain.UnitTests/
├── Eventy.Application.UnitTests/
├── Eventy.IntegrationTests/
└── Eventy.ConcurrencyTests/
```

**See**: [references/phase2-testing-foundation.md](references/phase2-testing-foundation.md) for complete foundation setup including SQL Server container factory, authentication mocks, and seed builders.

## Test Type Decision Tree

```
Adding a new feature?
├── Domain logic changed?     → Tier A: Write aggregate + value object tests first
├── Handler/command changed?  → Tier A: Write handler + behavior tests
├── API endpoint changed?     → Tier B: Write integration test for the full slice
├── Booking/ticket changed?   → Tier A + B + C: All three tiers
└── Infrastructure changed?   → Tier B: Integration test with real DB
```

## Code Quality Rules (All Tiers)

| Rule | Guideline |
|------|-----------|
| One behavior per test | `Reserve_WhenCapacityExceeded_ShouldReturnFailure` not `TestEverything` |
| Test public behavior | Never test private methods |
| Builder pattern | Use `EventBuilder`, `TicketBuilder`, `BookingBuilder` — never `new Event()` in tests |
| Mock interfaces | `IBookingRepository`, `IUnitOfWork` — never mock classes |
| Assert database state | HTTP responses can lie; always verify database reality |
| Deterministic tests | No random data, no `Thread.Sleep()`, no timing-dependent logic |
| Scenario naming | `LastTicketReservationTests`, `OversellingPreventionTests` — name by business risk |

## Technology Stack

```xml
<!-- Unit Tests -->
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="NSubstitute" Version="5.*" />
<PackageReference Include="FluentAssertions" Version="6.*" />

<!-- Integration Tests -->
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
<PackageReference Include="Testcontainers.MsSql" Version="4.*" />
<PackageReference Include="Respawn" Version="6.*" />
```

## CI/CD Pipeline Architecture

```
Pull Request
    ↓
Stage 1: Build Verification (dotnet restore + dotnet build)
    ↓
Stage 2: Unit Tests (< 30s, parallel, no Docker)
    ↓
Stage 3: Integration Tests (Testcontainers, full pipeline)
    ↓
Stage 4: Concurrency Validation (race condition protection)
    ↓
Stage 5: Quality Gate → Merge/Deploy

Nightly: Heavy stress tests (10,000+ concurrent users)
```

**See**: [references/phase6-cicd-quality-gates.md](references/phase6-cicd-quality-gates.md) for full pipeline configuration, coverage targets, and quality gate rules.

## Coverage Targets

| Layer | Target |
|-------|--------|
| Domain | 90%+ |
| Application | 80%+ |
| Infrastructure | 70%+ |
| Critical Booking Flow | 100% |

## Critical Test Matrix

| Area | Required Tests |
|------|---------------|
| TicketType Aggregate | Capacity invariant |
| Booking Aggregate | State transitions |
| CreateBooking Handler | Repository interaction |
| Validation Pipeline | Invalid commands rejected |
| EF Core | Migration correctness |
| Outbox | Event persistence with business data |
| API Endpoint | HTTP behavior |
| Concurrency | Last ticket race condition |
| Distributed Lock | Duplicate booking prevention |
| Stress | 500-5000 parallel users |

## Scaffolding Script

Use `scripts/scaffold-test-solution.py` to generate the complete test project structure, .csproj files, and folder layout. Run with the solution root path as argument.

## EF Core InMemory Warning

**Never use `UseInMemoryDatabase()` for Eventy tests.** It does not simulate:
- Real transactions (BEGIN/COMMIT/ROLLBACK)
- Database locks (row locks, deadlock detection)
- Query behavior (SELECT FOR UPDATE, isolation levels, indexes, constraints)

For ticketing systems: EF InMemory = unacceptable. Always use Testcontainers with real SQL Server/PostgreSQL.
