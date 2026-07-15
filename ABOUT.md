# About Eventiy

Eventiy is a production-grade event ticketing platform built on Clean Architecture (.NET 9 + Angular 19) that handles event creation, ticket inventory management, seat reservation with optimistic concurrency, payment processing (Stripe + Fawry deferred), real-time WebSocket seat synchronization, and asynchronous event-driven processing via the Transactional Outbox pattern.

## What the system does

- **Event lifecycle management** — Draft -> Published -> Cancelled/Completed state machine with admin override paths
- **Ticket inventory** — Per-ticket-type capacity tracking with Reserved/Sold/Available accounting enforced via domain invariants
- **Booking flow** — Pending -> Confirmed -> Cancelled -> Refunded state machine with Instant (Stripe) and Deferred (Fawry) payment methods
- **Real-time seat selection** — WebSocket-based seat locking with Redis Pub/Sub cross-instance broadcasting and D3.js SVG rendering
- **Asynchronous reliability** — Transactional Outbox with background polling, dead-letter queue, idempotency guards, and 3-attempt retry with exponential backoff
- **Caching layer** — Redis cache-aside for event lists (30s), event details (60s), and event photos (120s) with post-commit invalidation
- **Concurrency safety** — Row-level optimistic concurrency via SQL Server `rowversion`, 3-attempt retry on `ConcurrencyException`, pessimistic locking (`UPDLOCK, READPAST, ROWLOCK`) for background job dequeue
- **Geolocation** — Nearby-event queries with bounding box SQL filter + Haversine post-filter (~20km radius)
- **File uploads** — Event photo management with local storage (5MB, jpeg/png/webp), cover photo, reorder, caption

## Design approach

Eventiy follows:

- **Domain-Driven Design (DDD)** — 3 aggregate roots (Event, Booking, User), 3 entities (TicketType, EventPhoto, RefreshToken), 10 value objects (Money, Address, EventId, Email, Role, etc.), 20 domain events. Domain layer has zero external NuGet dependencies — pure DDD with no framework coupling.
- **Clean Architecture** — strict dependency direction: WebApi -> Application -> Domain; Infrastructure -> Application -> Domain. No business logic in controllers or infrastructure.
- **CQRS with MediatR** — 19 commands, 7 queries, 2 pipeline behaviors (Validation, Logging), separate read-side via `IApplicationReadDbContext` (AsNoTracking).
- **Result pattern** — all domain operations return `Result<T>` / `Result` with typed `Error[]` (Validation, NotFound, Conflict, Unauthorized, Failure) — never throw exceptions for expected business failures.
- **Transactional Outbox** — domain events extracted from aggregates in `UnitOfWork.CommitAsync`, staged in `OutboxMessages` table in the same DB transaction, dispatched asynchronously by `OutboxProcessor` (5s polling) with reflection-based handler resolution via `IDomainEventHandler<T>`.
- **Idempotency** — 4-level guard: `ProcessedEvents` table, event handler checks, webhook deterministic keys, outbox unique `IdempotencyKey` index.

## Architecture at a glance

```
Domain/           67 .cs files, 0 NuGet deps — pure DDD
Application/      98 .cs files, MediatR 14.1 + FluentValidation 12.1
Infrastructure/   EF Core 9, Redis, Stripe, BCrypt, WebSockets
Eventy.WebApi/    7 controllers, 3 middleware, minimal hosting
EventiyApp/       Angular 19 standalone, D3.js, signals, Tailwind
tests/            ~111 tests: domain unit, application unit, integration, concurrency
```

## Main domain concepts

- **Event** — aggregate root for an event with capacity, ticket types, photos, status machine, and seat operations (reserve/release/confirm/refund)
- **TicketType** — entity within Event aggregate; tracks Capacity, SoldCount, ReservedCount; all mutation methods are `internal`
- **Booking** — aggregate root with Pending -> Confirmed/Cancelled/Expired -> Refunded state machine; Instant (2-min hold) and Deferred (30-min hold, Fawry reference code) payment methods
- **User** — aggregate root with role-based access (Attendee, Organizer, Admin), refresh token rotation with reuse detection
- **Value objects** — Money (currency-validated, rounded), Address (with geolocation), strongly-typed IDs (EventId, BookingId, etc.), Email, Role (smart enum)

## Key infrastructure

- **Unit of Work** — extracts domain events from aggregates, stages them in outbox, saves all atomically in one DB transaction
- **Outbox pattern** — 5s polling, 50-message batches, `UPDLOCK/READPAST/ROWLOCK` locking, 3-retry exponential backoff (5s, 1min, dead letter)
- **Redis caching** — Singleton `ConnectionMultiplexer`, graceful degradation (never crashes on Redis failure), `cache:` key prefix, post-commit invalidation
- **Background jobs** — OutboxProcessor (5s), BookingExpirationJob (1min), PaymentReconciliationJob (2min)
- **Real-time** — WebSocket connection manager + Redis Pub/Sub broadcaster for seat-state deltas across API instances
- **Payments** — Stripe Checkout (production) / Mock gateway (dev), payment-before-commit ordering with `CancelPaymentAsync` compensation on failure

## Testing

3-tier pyramid with concurrency engine:
- **Domain unit tests** (~58) — pure domain logic, no mocks
- **Application unit tests** (15) — handler isolation with NSubstitute
- **Integration tests** (~28) — Testcontainers (SQL Server 2022), Respawn, WebApplicationFactory, fakes for Redis/Stripe
- **Concurrency tests** (~10) — barrier-synchronized parallel HTTP, up to 1000 concurrent users, verifies inventory invariants

## Project vision

Eventiy is a full event booking platform with organizer workflows, customer booking flows, payments, real-time seat selection, and clear domain rules that remain easy to maintain as the product expands. The architecture supports horizontal scaling (Redis Pub/Sub for cross-instance broadcasting), flash-sale scenarios (optimistic concurrency with retry, write-behind cache potential), and production observability (correlation IDs, structured logging, OpenTelemetry-ready).
