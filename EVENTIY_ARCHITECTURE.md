# EVENTIY — Enterprise Architecture Specification & End-to-End Workflow Trace

> **Document Classification:** Technical Architecture Specification
> **System:** Eventiy — Event Ticketing Platform
> **Stack:** .NET 9 Clean Architecture + Angular 19 (Standalone Signals)
> **Database:** SQL Server 2022 (EF Core 9, rowversion optimistic concurrency)
> **Caching:** Redis (StackExchange.Redis 2.8.31, cache-aside with graceful degradation)
> **Payments:** Stripe Checkout (with mock gateway for development)
> **Real-Time:** WebSockets + Redis Pub/Sub (seat-state synchronization)

---

## TABLE OF CONTENTS

1. [Executive Overview](#1-executive-overview)
2. [Clean Architecture Solution Topology](#2-clean-architecture-solution-topology)
3. [Domain Layer — Pure DDD Tactical Patterns](#3-domain-layer--pure-ddd-tactical-patterns)
4. [Application Layer — CQRS, MediatR, Pipelines & Result Pattern](#4-application-layer--cqrs-mediatr-pipelines--result-pattern)
5. [Infrastructure Layer — Persistence, Outbox, Caching, Payments, Real-Time](#5-infrastructure-layer--persistence-outbox-caching-payments-real-time)
6. [WebApi Presentation Layer — Controllers, Middleware, WebSocket Gateway](#6-webapi-presentation-layer--controllers-middleware-websocket-gateway)
7. [Angular Frontend — Standalone Components, Signals, D3 Seating Engine](#7-angular-frontend--standalone-components-signals-d3-seating-engine)
8. [Testing Architecture — 3-Tier Pyramid with Concurrency Engine](#8-testing-architecture--3-tier-pyramid-with-concurrency-engine)
9. [Injected Enterprise Components (Forgotten Pieces)](#9-injected-enterprise-components-forgotten-pieces)
10. [Exhaustive Cross-Layer Pattern Matrix](#10-exhaustive-cross-layer-pattern-matrix)
11. [End-to-End Workflow Trace: Ticket Booking / حجز التذاكر](#11-end-to-end-workflow-trace-ticket-booking--حجز-التذاكر)
12. [Payment Reliability Posture](#12-payment-reliability-posture)
13. [Security Hardening Posture](#13-security-hardening-posture)
14. [Key Decisions & Architectural Rationale](#14-key-decisions--architectural-rationale)


---

## 1. Executive Overview

Eventiy is a production-grade event ticketing platform built on Clean Architecture principles with strict Domain-Driven Design (DDD) tactical patterns. The system handles event creation, ticket inventory management, seat reservation with optimistic concurrency, payment processing (Stripe Checkout + Fawry deferred), real-time WebSocket seat synchronization, and asynchronous event-driven processing via the Transactional Outbox pattern.

### Core Capabilities
- **Event Lifecycle:** Draft -> Published -> Cancelled/Completed with admin override paths
- **Ticket Inventory:** Per-ticket-type capacity tracking with Reserved/Sold/Available accounting, enforced via invariants in the `TicketType` entity
- **Booking Flow:** Pending -> PendingPayment -> Confirmed -> Cancelled -> Refunded state machine, with Instant (Stripe) and Deferred (Fawry) payment methods
- **Real-Time Seat Selection:** WebSocket-based seat locking with Redis Pub/Sub cross-instance broadcasting, D3.js SVG rendering on the frontend
- **Asynchronous Reliability:** Transactional Outbox with background polling, durable compensation logs, dead-letter queues, idempotency guards, and retry backoff
- **Caching Layer:** Redis cache-aside for event lists (30s TTL), event details (60s TTL), and event photos (120s TTL) — all with post-commit invalidation
- **Concurrency Safety:** Row-level optimistic concurrency via SQL Server `rowversion`, 3-attempt retry loop on `ConcurrencyException`, pessimistic locking (`UPDLOCK, READPAST, ROWLOCK`) for background job dequeue

### Project Constraints
- **Domain purity:** Zero external NuGet dependencies — no EF Core, no MediatR, no ASP.NET, no `TimeProvider`. Time is passed as `DateTime utcNow` parameters from the Application layer.
- **No authorization in domain:** Role checks are commented out in domain error classes. Authorization enforced via `[Authorize]` attributes on controllers and `ICurrentUserService` in handlers.
- **Build verification:** Must compile with 0 errors on both `dotnet build Eventy.WebApi` and `ng build`.
- **No indirection without value:** Direct `new XxxEvent(...)` calls instead of static factory wrappers.

---

## 2. Clean Architecture Solution Topology

### Solution Structure

```
Eventiy/
├── Domain/                          (68 .cs files, 0 NuGet deps)
│   ├── Abstractions/                (4 repo interfaces + 1 storage interface)
│   ├── Aggregates/                   (3 aggregate roots + 3 entities)
│   ├── Common/                       (11 base types: AggregateRoot, Entity, ValueObjectBase, Result, Error, etc.)
│   ├── Errors/                       (7 static error factory classes)
│   └── Primitives/                   (Money, Address value objects)
│
├── Application/                      (100+ .cs files, deps: Domain + MediatR 14.1 + FluentValidation 12.1)
│   ├── Abstractions/                (16+ interfaces across 8 categories)
│   ├── Features/
│   │   ├── Authentication/          (4 commands + AuthResponse)
│   │   ├── Bookings/                 (5 commands + 4 queries + 2 event handlers + 1 validator)
│   │   └── Events/                   (10 commands + 3 queries)
│   └── DependencyInjection.cs
│
├── Infrastructure/                   (deps: Application + Domain + EF Core 9 + Redis + Stripe + BCrypt)
│   ├── Authentication/              (JwtTokenGenerator, PasswordHasher, CurrentUserService, JwtSettings)
│   ├── BackgroundJobs/              (OutboxProcessor, CompensationProcessor, BookingExpirationJob, PaymentReconciliationJob, OutboxDispatcher)
│   ├── Caching/                     (RedisCacheService — Singleton, graceful degradation)
│   ├── Messaging/                   (EventMetadataFactory — correlation/causation)
│   ├── Migrations/                  (schema migrations including CompensationLogs, outbox/dead-letter tables)
│   ├── Payments/                    (StripePaymentGateway, MockPaymentGateway, StripeSettings)
│   ├── Persistence/
│   │   ├── ApplicationDbContext.cs
│   │   ├── UnitOfWork.cs
│   │   ├── Configuration/           (9 Fluent API configs)
│   │   ├── Outbox/                   (OutboxMessage, OutboxDeadLetter, CompensationLog, EventSerializer, JSON converters)
│   │   ├── Repositories/            (7 repos: Event, Booking, User, EventPhoto, Outbox, CompensationLog, IdempotencyStore)
│   │   └── SeedData/                (DatabaseSeeder — 18 events, 12 users, ~75 bookings)
│   ├── RealTime/                    (WebSocketConnectionManager, RedisPubSubBroadcaster, SeatStateDelta)
│   ├── Services/                    (VenueLayoutValidator)
│   └── Storage/                     (LocalFileStorageService)
│
├── Eventy.WebApi/                    (14 .cs files, deps: Application + Domain + Infrastructure + OpenAPI + Scalar)
│   ├── Controllers/                 (7 controllers, ~35 endpoints)
│   ├── Middlewares/                 (CorrelationId, GlobalExceptionHandling, WebSocket)
│   ├── Extensions/                  (ResultExtensions — Result to IActionResult mapping)
│   └── Program.cs                   (88-line minimal hosting model)
│
├── EventiyApp/                       (Angular 19, 67 .ts files)
│   ├── src/app/core/                (models, enums, mappers, guards)
│   ├── src/app/application/         (7 HTTP services + 4 application services)
│   ├── src/app/infrastructure/      (2 interceptors, 1 directive, 2 services)
│   ├── src/app/shared/              (8 shared components + 1 pipe)
│   ├── src/app/features/            (15 feature components + D3 seating chart engine)
│   └── src/app/presentation/        (error pages)
│
└── tests/                            (44 .cs files, ~149 test methods)
    ├── Eventy.Domain.UnitTests/     (8 test files, ~74 tests — pure domain logic)
    ├── Eventy.Application.UnitTests/ (5 test files, ~31 tests — handler isolation with NSubstitute)
    ├── Eventy.IntegrationTests/     (10 test files, ~34 tests — Testcontainers + Respawn)
    ├── Eventy.ConcurrencyTests/     (5 test files, ~10 tests — barrier-synchronized parallel HTTP)
    └── Eventy.Testing.Foundation/   (10 shared files — WebApplicationFactory, fakes, builders, container factory)
```

### Dependency Direction (Strict Clean Architecture)

```
WebApi ──> Application ──> Domain
  │            │
  └──> Infrastructure ──> Application
                    │
                    └──> Domain
```

- **Domain** has zero outward dependencies (only .NET BCL)
- **Application** depends only on Domain + MediatR + FluentValidation
- **Infrastructure** depends on Application + Domain + all I/O packages (EF Core, Redis, Stripe, BCrypt)
- **WebApi** depends on all three layers but contains no business logic — only controllers, middleware, and HTTP mapping

### NuGet Dependency Matrix

| Project | Key Packages | Purpose |
|---------|-------------|---------|
| Domain | *(none)* | Pure DDD — zero framework coupling |
| Application | MediatR 14.1.0, FluentValidation 12.1.1 | CQRS pipeline, validation |
| Infrastructure | EF Core 9.0.0 (SqlServer), StackExchange.Redis 2.8.31, Stripe.net 52.1.0, BCrypt.Net-Next 4.2.0, JwtBearer 9.0.0 | All I/O concerns |
| WebApi | Microsoft.AspNetCore.OpenApi 9.0.16, Scalar.AspNetCore 2.16.3 | API documentation |
| Testing.Foundation | Testcontainers.MsSql 4.0.0, Respawn 6.0.0, Mvc.Testing 9.0.0 | Test infrastructure |
| Frontend | Angular 19.1, D3 7.9, CDK 19.0, Tailwind 3.4, RxJS 7.8 | UI rendering, SVG venue, drag-drop |

---

## 3. Domain Layer — Pure DDD Tactical Patterns

The Domain layer contains **67 .cs source files** with **zero external NuGet dependencies**. It targets `net9.0` with implicit usings and nullable enabled. Every domain operation receives `DateTime utcNow` as a parameter from the Application layer — the domain never calls `DateTime.UtcNow` directly (with one minor exception: `RefreshToken.IsActive`).

### 3.1 Common Base Types (`Domain/Common/`)

#### AggregateRoot<TId>
- **File:** `Domain/Common/AggregateRoot.cs`
- **Inherits:** `Entity<TId>`, implements `IAggregateRoot`
- **Type constraint:** `TId : ValueObjectBase`
- **Key members:**
  - `_domainEvents` — `List<IDomainEvent>` (private collection of pending domain events)
  - `DomainEvents` — `IReadOnlyList<IDomainEvent>` (read-only accessor)
  - `RaiseDomainEvent(IDomainEvent)` — protected method; adds event to the collection
  - `ClearDomainEvents()` — public method; flushes events after `UnitOfWork.CommitAsync` extracts them
- **Construction:** Two constructors — `protected AggregateRoot(TId id)` for new aggregates, `protected AggregateRoot()` for EF Core materialization

#### Entity<TId>
- **File:** `Domain/Common/Entity.cs` (namespace is `Domain.Primitives` — a quirk)
- **Type constraint:** `TId : ValueObjectBase`
- **Identity equality:** Two entities are equal only if same type AND same `Id`. `GetHashCode()` returns `Id.GetHashCode()`. Reference equality checked first via `ReferenceEquals`.

#### ValueObjectBase
- **File:** `Domain/Common/ValueObjectBase.cs`
- **Structural equality:** `GetEqualityComponents()` yields the components that define equality. Two VOs are equal if all components match via `SequenceEqual`. `GetHashCode` uses XOR aggregate of component hashes (null-safe).

#### Result / Result<TValue>
- **File:** `Domain/Common/Result.cs`
- **Purpose:** Result pattern for returning success/failure without throwing exceptions. All domain factory methods and state transitions return `Result<T>` or `Result`.
- **Key invariants:**
  - Successful result cannot contain errors (throws `ArgumentException`)
  - Failed result must contain at least one error (throws `ArgumentException`)
  - `Result<TValue>.Value` throws `InvalidOperationException` if accessed on a failed result
  - Implicit conversion from `TValue` to `Result<TValue>` (null -> `Failure(Error.NullValue)`)

#### Error (record)
- **File:** `Domain/Common/Error.cs`
- **Signature:** `sealed record Error(string Code, string Message, ErrorType Type)`
- **ErrorType enum:** `Failure=1, Validation=2, NotFound=3, Conflict=4, Unauthorized=5`
- **Factory methods:** `Validation()`, `NotFound()`, `Conflict()`, `Failure()`, `Unauthorized()`

#### IDomainEvent / DomainEvent
- **File:** `Domain/Common/IDomainEvent.cs`, `Domain/Common/DomainEvent.cs`
- **Interface members:** `Guid Id`, `string Name`, `string Domain`, `DateTime OccurredOnUtc`
- **Static method:** `GetDomainEventName<T>()` returns `typeof(T).Name`
- **Abstract base:** `Id` is auto-generated `Guid.NewGuid()` (regenerated on each deserialization — this is why idempotency uses the domain entity ID, not `DomainEvent.Id`)

#### ConcurrencyException
- **File:** `Domain/Common/ConcurrencyException.cs`
- Sealed exception wrapping `DbUpdateConcurrencyException` at the infrastructure layer. Thrown by `UnitOfWork.CommitAsync()` when EF Core detects a `rowversion` mismatch.

#### DomainEventHandlerException
- **File:** `Domain/Common/DomainEventHandlerException.cs`
- Sealed exception carrying `EventType` (string) and `Errors` (string[]). Used by `OutboxDispatcher` to propagate handler failures.

#### IValidationResult<TSelf>
- **File:** `Domain/Common/IValidationResult.cs`
- Self-referential generic interface with a static abstract method `CreateFailure(Error[])`. Used by `ValidationPipelineBehavior` for type-safe failure construction without reflection.

### 3.2 Primitives (`Domain/Primitives/`)

#### Money — Value Object
- **File:** `Domain/Primitives/Money.cs`
- **Properties:** `Amount` (decimal, rounded to 2 places via `MidpointRounding.AwayFromZero`), `Currency` (string, 3 uppercase letters)
- **Factory:** `Create(decimal amount, string currency)` — validates: amount >= 0, currency not empty, exactly 3 chars, all letters
- **Arithmetic (all return Result<Money>):** `Add`, `Subtract` (rejects negative result), `Multiply` (rejects negative multiplier), `Divide` (rejects divisor <= 0), `Percentage`, `AddPercentage`, `SubtractPercentage`
- **Comparison (all return Result<bool>):** `IsGreaterThan`, `IsLessThan`, `IsGreaterThanOrEqual`, `IsLessThanOrEqual`, `IsEqualTo` — all reject null operands and currency mismatch
- **Implicit conversion:** `operator decimal(Money)` — enables transparent use in arithmetic contexts
- **Invariants enforced:**
  - Amount is always rounded to 2 decimal places (away-from-zero midpoint)
  - Currency is always 3 uppercase letters
  - All arithmetic operations validate same-currency before proceeding
  - Subtraction cannot produce negative amounts

#### Address — Value Object
- **File:** `Domain/Primitives/Address.cs`
- **Properties:** `Country`, `City`, `Street` (required, trimmed), `PostalCode` (nullable), `Latitude` (double?, -90 to 90), `Longitude` (double?, -180 to 180)
- **Constants:** `MAX_COUNTRY_LENGTH=100`, `MAX_CITY_LENGTH=100`, `MAX_STREET_LENGTH=200`, `MAX_POSTAL_CODE_LENGTH=20`
- **Methods:** `IsEmpty()`, `GetFullAddress()` (Street, City, Country [, PostalCode]), `GetShortAddress()` (City, Country)
- **Invariants:** All required fields non-empty and within max lengths; all string inputs trimmed; lat/lng range validated

### 3.3 Event Aggregate (`Domain/Aggregates/EventAggregate/`)

#### Event — Aggregate Root
- **File:** `Domain/Aggregates/EventAggregate/Event.cs`
- **Inherits:** `AggregateRoot<EventId>`
- **Properties:** `EventName`, `Capacity`, `Date`, `Location` (Address), `Status` (EventStatus), `Type` (EventType), `Description`, `PublishedAt?`, `CancelledAt?`, `CompletedAt?`, `CancellationReason?`, `CreatedAt`, `LastModifiedAt?`, `RowVersion` (byte[])
- **Collections:** `TicketTypes` (IReadOnlyCollection), `Photos` (IReadOnlyList)
- **Constants:** `MAX_TICKET_TYPES=10`, `MAX_PHOTOS=10`, `MAX_NAME_LENGTH=100`, `MAX_DESCRIPTION_LENGTH=500`, `MIN_CAPACITY=1`

**Factory — `Create(...)`:**
- Validates capacity >= 1, date in future, location non-null, description length, name via `EventName.Create`
- Re-validates address fields
- Sets `Status = Draft`, `CreatedAt = utcNow`
- Raises `EventCreatedEvent`

**Lifecycle State Machine:**

| Method | Precondition | Postcondition | Event Raised |
|--------|-------------|---------------|--------------|
| `Publish(utcNow)` | Draft + has ticket types + date in future | Published | `EventPublishedEvent` |
| `Cancel(utcNow, reason?)` | Draft or Published (not Cancelled/Completed) | Cancelled | `EventCancelledEvent` |
| `AdminCancel(utcNow, reason?)` | Any non-Completed (admin override) | Cancelled | `EventCancelledEvent` |
| `Complete(utcNow)` | Published + date in past | Completed | `EventCompletedEvent` |
| `Reopen(utcNow)` | Completed + date in future | Published | *(no specific event)* |

**Ticket Type Management (Draft-only, except admin overrides):**
- `AddTicketType(...)` — max 10, no duplicate names (case-insensitive), capacity <= remaining event capacity; raises `TicketTypeAddedEvent`
- `UpdateTicketTypePrice(...)` — raises `TicketTypePriceUpdatedEvent`
- `UpdateTicketTypeCapacity(...)` — validates remaining event capacity; raises `TicketTypeCapacityUpdatedEvent`
- `RemoveTicketType(...)` — delegates to `TicketType.Remove()` (rejects if sold/reserved > 0); raises `TicketTypeRemovedEvent`

**Seat Operations (available in any status):**
- `ReserveSeats(ticketTypeId, quantity, utcNow)` — delegates to `TicketType.ReserveSeats`; raises `TicketTypeSeatsReservedEvent`
- `ReleaseSeats(...)` — delegates to `TicketType.ReleaseSeats`; raises `TicketTypeSeatsReleasedEvent`
- `ConfirmReservation(...)` — delegates to `TicketType.ConfirmReservation`; raises `TicketTypeSeatsReservedEvent` (reuses Reserved event)
- `RefundSeats(...)` — delegates to `TicketType.RefundSeats`; raises `TicketTypeSeatsReleasedEvent` (reuses Released event)

**Admin Override Methods (bypass Draft restriction):**
- `AdminUpdateName`, `AdminUpdateCapacity`, `AdminUpdateDate`, `AdminUpdateDescription`, `AdminUpdateLocation`, `AdminAddTicketType`, `AdminCancel`

**Photo Management:**
- `AddPhoto(EventPhoto, utcNow)` — max 10, auto-sets first photo as cover; raises `EventPhotosUpdatedEvent` (action="Added")
- `RemovePhoto(EventPhotoId, utcNow)` — raises `EventPhotosUpdatedEvent` (action="Removed")
- `SetCoverPhoto(EventPhotoId, utcNow)` — clears all other covers; raises `EventPhotosUpdatedEvent` (action="CoverChanged")
- `UpdatePhotoCaption(...)` — raises `EventPhotosUpdatedEvent` (action="CaptionUpdated")
- `ReorderPhotos(List<EventPhotoId>, utcNow)` — validates count match and all IDs belong to event; raises `EventPhotosUpdatedEvent` (action="Reordered")

**Query Methods:** `GetRemainingCapacity`, `GetReservedCount`, `GetAvailableSeats`, `HasAvailableSeats`, `GetTotalSoldCount`, `GetTotalTicketCapacity`, `IsFullyBooked`, `GetOccupancyRate`

#### TicketType — Entity (internal to Event aggregate)
- **File:** `Domain/Aggregates/EventAggregate/Entities/TicketType.cs`
- **Inherits:** `Entity<TicketTypeId>`
- **Properties:** `EventId`, `TicketTypeName`, `Price` (Money), `Capacity`, `SoldCount`, `ReservedCount`, `SectionCode?`, `VenueType?`, `RowVersion`, `CreatedAt`, `LastModifiedAt?`
- **Computed properties:** `AvailableCount` (= Capacity - Sold - Reserved), `OccupancyRate`, `ReservationRate`, `UnavailableCount` (= Sold + Reserved)

**Seat Lifecycle (internal methods — only Event aggregate can call):**
- `ReserveSeats(quantity, utcNow)` — validates quantity > 0 and <= AvailableCount; increments ReservedCount
- `ReleaseSeats(quantity, utcNow)` — validates quantity > 0 and <= ReservedCount; decrements ReservedCount
- `ConfirmReservation(quantity, utcNow)` — validates quantity > 0, <= ReservedCount, and SoldCount + quantity <= Capacity; increments SoldCount, decrements ReservedCount
- `SellDirect(quantity, utcNow)` — validates quantity > 0, <= AvailableCount; increments SoldCount directly (bypass reservation)
- `RefundSeats(quantity, utcNow)` — validates quantity > 0, <= SoldCount; decrements SoldCount
- `Remove()` — validates SoldCount == 0 AND ReservedCount == 0

**Invariants enforced:**
- All mutation methods are `internal` (only Event aggregate can call them)
- Cannot reserve more than available
- Cannot release more than reserved
- Cannot confirm more than reserved
- Cannot exceed capacity on confirm
- Cannot refund more than sold
- Cannot remove ticket type with sold or reserved tickets

#### EventPhoto — Entity (sealed)
- **File:** `Domain/Aggregates/EventAggregate/Entities/EventPhoto.cs`
- **Properties:** `EventId`, `FileName`, `StoragePath`, `PublicUrl`, `Caption?`, `DisplayOrder`, `IsCover`, `UploadedAt`
- **Constants:** `MAX_FILE_NAME_LENGTH=255`, `MAX_CAPTION_LENGTH=500`, `MAX_STORAGE_PATH_LENGTH=1000`, `MAX_PUBLIC_URL_LENGTH=1000`
- **Methods:** `SetCover()`, `RemoveCover()`, `UpdateCaption(string?)`, `UpdateDisplayOrder(int)`
- **Invariants:** Cover flag mutually exclusive (managed by Event.SetCoverPhoto); display order always non-negative; caption trimmed or null

### 3.4 Booking Aggregate (`Domain/Aggregates/BookingAggregate/`)

#### Booking — Aggregate Root
- **File:** `Domain/Aggregates/BookingAggregate/Booking.cs`
- **Inherits:** `AggregateRoot<BookingId>`
- **Properties:** `UserId`, `EventId`, `TicketTypeId`, `EventTitle`, `Quantity`, `BookingDate`, `Status` (BookingStatusEnum), `Money`, `TotalAmount`, `HoldExpiresAt?`, `ConfirmationDate?`, `CancellationDate?`, `RefundDate?`, `CancellationReason?`, `PaymentMethod`, `ReferenceCode?`, `RowVersion?`
- **Constants:** `MAX_QUANTITY_PER_BOOKING=10`, `REFUND_PERIOD_DAYS=7`

**Factory — `Create(...)`:**
- Validates quantity (1-10), event title non-empty, money non-null and > 0
- Sets `Status = Pending`, calculates `TotalAmount = money.Amount * quantity`
- **Payment method behavior in constructor:**
  - `Deferred` -> generates `ReferenceCode` (format `FAW-{hex}`) using `RandomNumberGenerator.Fill` (cryptographically secure), `HoldExpiresAt = utcNow + 30 min`
  - `Instant` -> `HoldExpiresAt = utcNow + 2 min`
- Raises `BookingCreatedEvent`

**State Machine:**

| Method | Precondition | Postcondition | Event Raised |
|--------|-------------|---------------|--------------|
| `MarkAsPendingPayment(utcNow)` | Pending | PendingPayment | `PaymentInitiatedEvent` |
| `MarkAsPending(utcNow)` | PendingPayment | Pending | — |
| `Confirm(utcNow)` | Pending or PendingPayment | Confirmed | `BookingConfirmedEvent` |
| `Cancel(utcNow, reason?)` | Pending, PendingPayment, or Confirmed | Cancelled | `BookingCancelledEvent` |
| `RequestCancellation(utcNow, reason?)` | Confirmed -> admin approval; Pending -> delegates to `Cancel()` | (raises `BookingCancellationRequestedEvent` for Confirmed) |
| `Refund(utcNow)` | Cancelled + within 7-day window | Refunded | `BookingRefundedEvent` |
| `Expire(utcNow)` | Pending/PendingPayment + HoldExpiresAt has passed | Expired | `BookingExpiredEvent` |
| `UpdateQuantity(quantity, utcNow)` | Pending | recalculates TotalAmount | `BookingQuantityUpdatedEvent` |

### 3.5 User Aggregate (`Domain/Aggregates/UserAggregate/`)

#### User — Aggregate Root
- **File:** `Domain/Aggregates/UserAggregate/User.cs`
- **Inherits:** `AggregateRoot<UserId>`
- **Properties:** `FirstName`, `LastName`, `Email`, `PasswordHash`, `Role`, `IsApproved`, `RowVersion?`, `FullName` (computed), `RefreshTokens` (IReadOnlyCollection)
- **Factory:** `Create(...)` — trims names, generates UserId, raises `UserRegisteredEvent`
- **Methods:** `Approve()`, `GetPasswordHash()`, `IssueRefreshToken(tokenHash, expires, utcNow)`, `RevokeRefreshToken(tokenHash, utcNow)`, `RevokeAllRefreshTokens(utcNow)` (used on refresh-token reuse detection)
- **Note:** `IsApproved` defaults to `true` (organizer approval handled externally via `ApproveOrganizerCommand`)

#### RefreshToken — Entity (sealed, not an aggregate root)
- **Properties:** `Id` (int), `TokenHash`, `ExpiresOnUtc`, `CreatedOnUtc`, `RevokedOnUtc?`, `ReplacedByTokenHash?`, `IsActive` (computed: `RevokedOnUtc is null && DateTime.UtcNow < ExpiresOnUtc`)
- **Minor inconsistency:** `IsActive` uses `DateTime.UtcNow` directly — the only place in the domain that doesn't receive `DateTime utcNow` as a parameter

### 3.6 Value Objects (ID Types)

All ID value objects inherit from `ValueObjectBase` and have a `FromDatabase(Guid)` factory that bypasses validation (used by EF Core materialization). The `Create(Guid)` factory is used for new IDs and enforces `Guid.Empty` rejection.

| VO | File | Wraps | Validation |
|----|------|-------|------------|
| `EventId` | `EventAggregate/ValueObject/EventId.cs` | `Guid Value` | Rejects `Guid.Empty` |
| `EventName` | `EventAggregate/ValueObject/EventName.cs` | `string Value` | Rejects empty/whitespace, max 100 chars, trims |
| `EventPhotoId` | `EventAggregate/ValueObject/EventPhotoId.cs` | `Guid Value` | Rejects `Guid.Empty` |
| `TicketTypeId` | `EventAggregate/ValueObject/TicketTypeId.cs` | `Guid Value` | Rejects `Guid.Empty` |
| `BookingId` | `BookingAggregate/ValueObject/BookingId.cs` | `Guid Value` | Rejects `Guid.Empty`; implicit `operator Guid` |
| `UserId` | `UserAggregate/ValueObject/UserId.cs` | `Guid Value` | Rejects `Guid.Empty` |
| `Email` | `UserAggregate/ValueObject/Email.cs` | `string Value` | Regex `^[^@\s]+@[^@\s]+\.[^@\s]+$`, max 256, lowercased |
| `Role` | `UserAggregate/ValueObject/Role.cs` | Smart Enum | `Attendee`, `Organizer`, `Admin`; `FromString(string)` returns `Result<Role>` |

### 3.7 Domain Events (21 total)

All events inherit from `DomainEvent` (abstract) and implement `IDomainEvent`. Each sets `Domain` to its aggregate name and `Name` to its type name.

**Event Aggregate Events (6 + 9 TicketType = 15):**

| Event | Payload | Raised By |
|-------|---------|-----------|
| `EventCreatedEvent` | EventId, EventName, Date, Capacity | `Event.Create()` |
| `EventPublishedEvent` | EventId, PublishedAt, TotalTicketTypes | `Event.Publish()` |
| `EventCancelledEvent` | EventId, CancelledAt, Reason? | `Event.Cancel()` / `Event.AdminCancel()` |
| `EventCompletedEvent` | EventId, CompletedAt | `Event.Complete()` |
| `EventCapacityUpdatedEvent` | EventId, OldCapacity, NewCapacity, UpdatedAt | `Event.UpdateCapacity()` / `Event.AdminUpdateCapacity()` |
| `EventPhotosUpdatedEvent` | EventId, Action, PhotoCount | All photo operations |
| `TicketTypeAddedEvent` | EventId, TicketTypeId, TicketName, Price, Capacity | `Event.AddTicketType()` |
| `TicketTypeCapacityUpdatedEvent` | TicketTypeId, EventId, OldCapacity, NewCapacity, UpdatedAt | `Event.UpdateTicketTypeCapacity()` |
| `TicketTypePriceUpdatedEvent` | TicketTypeId, EventId, OldPrice, NewPrice, Currency, UpdatedAt | `Event.UpdateTicketTypePrice()` |
| `TicketTypeRemovedEvent` | TicketTypeId, EventId, TicketTypeName, RemovedAt | `Event.RemoveTicketType()` |
| `TicketTypeSeatsReservedEvent` | TicketTypeId, EventId, QuantityReserved, TotalSold, AvailableRemaining | `Event.ReserveSeats()` AND `Event.ConfirmReservation()` |
| `TicketTypeSeatsReleasedEvent` | TicketTypeId, EventId, QuantityReleased, TotalSold, AvailableRemaining | `Event.ReleaseSeats()` AND `Event.RefundSeats()` |

**Booking Aggregate Events (9):**

| Event | Payload | Raised By |
|-------|---------|-----------|
| `BookingCreatedEvent` | BookingId, UserId, EventId, TicketTypeId, Quantity, TotalAmount | `Booking.Create()` — has `[JsonConstructor]` for outbox deserialization |
| `PaymentInitiatedEvent` | BookingId, UserId, EventId, TicketTypeId, InitiatedAt | `Booking.MarkAsPendingPayment()` |
| `BookingConfirmedEvent` | BookingId, UserId, EventId, TicketTypeId, Quantity, ConfirmedAt | `Booking.Confirm()` |
| `BookingCancelledEvent` | BookingId, UserId, EventId, TicketTypeId, Quantity, Reason? | `Booking.Cancel()` |
| `BookingCancellationRequestedEvent` | BookingId, UserId, EventId, Reason? | `Booking.RequestCancellation()` (confirmed bookings) |
| `BookingExpiredEvent` | BookingId, UserId, EventId, TicketTypeId, Quantity | `Booking.Expire()` |
| `BookingRefundedEvent` | BookingId, UserId, EventId, RefundAmount, RefundedAt | `Booking.Refund()` |
| `BookingQuantityUpdatedEvent` | BookingId, OldTotalAmount, NewTotalAmount, UpdatedAt | `Booking.UpdateQuantity()` |
| `BookingHeldEvent` | BookingId, UserId, EventId, TicketTypeId, Quantity, HoldExpiresAt | *(defined but never raised — orphaned)* |

**User Aggregate Events (1):**

| Event | Payload | Raised By |
|-------|---------|-----------|
| `UserRegisteredEvent` | UserId, Email | `User.Create()` |

### 3.8 Enums (4)

| Enum | File | Values |
|------|------|--------|
| `EventStatus` | `EventAggregate/Enums/EventStatus.cs` | `Draft`, `Published`, `Cancelled`, `Completed` |
| `EventType` | `EventAggregate/Enums/EventType.cs` | `Music`, `Tech`, `Sports`, `Art`, `Food`, `Education`, `Theater`, `Outdoors` |
| `BookingStatusEnum` | `BookingAggregate/Enums/BookingStatusEnum.cs` | `Pending=0`, `Confirmed=1`, `Cancelled=2`, `Expired=3`, `Refunded=4`, `PendingPayment=5` |
| `PaymentMethod` | `BookingAggregate/Enums/PaymentMethodEnum.cs` | `Instant=0`, `Deferred=1` |

### 3.9 Repository Interfaces (`Domain/Abstractions/Persistence/`)

| Interface | File | Key Methods |
|-----------|------|-------------|
| `IEventRepository` | `IEventRepository.cs` | `AddEventAsync(Event, ct)`, `GetByIdAsync(EventId, ct)` |
| `IBookingRepository` | `IBookingRepository.cs` | `AddBookingAsync(Booking, ct)`, `GetByIdAsync(BookingId, ct)`, `GetExpiredPendingBookingsAsync(utcNow, batchSize, ct) [Pending/PendingPayment]`, `GetPendingInstantBookingsPastHoldAsync(utcNow, batchSize, ct)`, `GetByReferenceCodeAsync(string, ct)` |
| `IEventPhotoRepository` | `IEventPhotoRepository.cs` | `GetByEventIdAsync(EventId, ct)`, `GetByIdAsync(EventPhotoId, ct)`, `Add(EventPhoto)`, `Update(EventPhoto)`, `Delete(EventPhoto)` |
| `IUserRepository` | `IUserRepository.cs` | `GetByEmailAsync(Email, ct)`, `GetByIdAsync(UserId, ct)`, `GetByRefreshTokenHashAsync(string, ct)`, `AddAsync(User, ct)` |

### 3.10 Service Abstractions

| Interface | File | Methods |
|-----------|------|---------|
| `IFileStorageService` | `Domain/Abstractions/Storage/IFileStorageService.cs` | `UploadAsync(Stream, fileName, contentType, ct)`, `DeleteAsync(storagePath, ct)`, `GetPublicUrl(storagePath)` |

### 3.11 Errors (`Domain/Errors/` — 7 static factory classes)

| Class | File | Error Count | Error Types |
|-------|------|-------------|-------------|
| `AddressErrors` | `AddressErrors.cs` | 9 | Validation |
| `EventErrors` | `EventErrors.cs` | ~35 | Validation, Conflict, NotFound |
| `EventPhotoErrors` | `EventPhotoErrors.cs` | 11 | Validation, Conflict, NotFound |
| `BookingErrors` | `BookingErrors.cs` | ~28 | Validation, Conflict, NotFound, Failure |
| `MoneyErrors` | `MoneyErrors.cs` | 12 | Validation |
| `TicketTypeErrors` | `TicketTypeErrors.cs` | ~25 | Validation, Conflict, NotFound |
| `UserErrors` | `UserErrors.cs` | 13 | Unauthorized, Validation, Conflict, NotFound |

**Note:** Several commented-out authorization errors exist in `BookingErrors.cs`, `EventErrors.cs`, and `TicketTypeErrors.cs` — deliberately removed to keep the domain unaware of authorization concerns.

---

## 4. Application Layer — CQRS, MediatR, Pipelines & Result Pattern

The Application layer contains **100+ .cs source files** and depends only on Domain + MediatR 14.1.0 + FluentValidation 12.1.1. No EF Core, no ASP.NET, no Redis packages — all infrastructure is abstracted behind interfaces.

### 4.1 CQRS Contracts (`Abstractions/Messaging/`)

| Interface | Base | Purpose |
|-----------|------|---------|
| `ICommand` | `IRequest<Result>` | Command with no return value |
| `ICommand<TResponse>` | `IRequest<Result<TResponse>>` | Command returning a value |
| `IQuery<TResponse>` | `IRequest<Result<TResponse>>` | Query (read side) |
| `ICommandHandler<TCommand>` | `IRequestHandler<TCommand, Result>` | Command handler |
| `ICommandHandler<TCommand, TResponse>` | `IRequestHandler<TCommand, Result<TResponse>>` | Command handler with return |
| `IQueryHandler<TQuery, TResponse>` | `IRequestHandler<TQuery, Result<TResponse>>` | Query handler |

### 4.2 Pipeline Behaviors

#### ValidationPipelineBehavior<TRequest, TResponse>
- **File:** `Abstractions/Behaviors/ValidationPipelineBehavior.cs`
- **Constraint:** `where TResponse : IValidationResult<TResponse>`
- **Flow:** Injects `IEnumerable<IValidator<TRequest>>`. If no validators found, calls `next()` immediately. Otherwise runs all validators in parallel (`Task.WhenAll`), collects all failures, converts them to `Error.Validation(...)` objects, and returns `TResponse.CreateFailure(errors)` — a type-safe factory method on the Result type (no reflection).

#### LoggingPipelineBehavior<TRequest, TResponse>
- **File:** `Abstractions/Behaviors/LoggingPipelineBehavior.cs`
- **Flow:** Logs `[START] Handling {RequestName}` before handler, `[END] {RequestName} handled in {ElapsedMs}ms` after handler. Uses `Stopwatch` for timing. Registered as transient.

### 4.3 All Abstractions / Interfaces (15 total)

#### Security Abstractions
- `IPasswordHasher` — `Hash(string)`, `Verify(string, string)`
- `IJwtTokenGenerator` — `GenerateToken(User)`, `GenerateRefreshToken()`, `HashToken(string)`
- `ICurrentUserService` — `GetCurrentUserId()`, `GetCurrentUserRole()`, `IsAuthenticated`

#### Caching Abstraction
- `ICacheService` — `GetAsync<T>(key, ct)`, `SetAsync<T>(key, value, ttl, ct)`, `RemoveAsync(key, ct)`, `RemoveByPatternAsync(pattern, ct)`, `ClearAllAsync(ct)`

#### Persistence Abstractions
- `IUnitOfWork` — `CommitAsync(ct)` (saves + stages outbox events atomically), `CommitWithoutEventsAsync(ct)` (save without event extraction)
- `IIdempotencyStore` — `IsProcessedAsync(Guid, ct)`, `MarkAsProcessedAsync(Guid, key, time, ct)` (synchronous save), `MarkAsProcessed(Guid, key, time)` (track-only — caller commits atomically)
- `IEventValidator<TEvent>` — `ValidateAsync(TEvent, ct)` — pre-processing validation for domain events
- `IDomainEventHandler<TEvent>` — `HandleAsync(TEvent, ct)` — dispatched by OutboxProcessor (not MediatR)
- `IApplicationReadDbContext` — `Query<T>()`, `ToListAsync`, `FirstOrDefaultAsync`, `AnyAsync`, `CountAsync` — read-side optimized (AsNoTracking)

#### Payment Abstraction
- `IPaymentService` — `InitiatePaymentAsync(bookingId, referenceCode, amount, currency, idempotencyKey, ct)`, `CancelPaymentAsync(bookingId, ct)` — returns `Result<PaymentInitiationResult>` / `Result`
- `CompensationLogDto` — Application-layer DTO for durable external cleanup records without referencing Infrastructure entities
- `ICompensationLogRepository` — lock, retry, mark-processed/failed, release-lock, and dead-letter operations for compensation records

#### Outbox Abstractions
- `IOutboxMessageService` — `AddFromDomainEvents(IEnumerable<IDomainEvent>)` — converts domain events into outbox messages
- `IOutboxProcessor` — `ProcessPendingMessagesAsync(ct)` — background service entry point
- `IOutboxRepository` — full outbox persistence contract (Add, GetAndLock, MarkProcessed, MarkFailed, ReleaseLock, GetCount, MoveToDeadLetter, GetDeadLetters, RequeueDeadLetter)
- `IOutboxDispatcher` — `DispatchBatchAsync(messages, lockId, timeProvider, ct)` — dispatch to handlers
- `IEventSerializer` — `Serialize<T>`, `Deserialize(eventName, payload)`, `Deserialize<T>(payload)`, type discovery helpers

#### Other Abstractions
- `IVenueLayoutValidator` — `HasVenueLayout(EventType)`, `GetValidSections(EventType)`, `ValidateSectionCode(EventType, sectionCode?)`
- `IEventMetadataFactory` — `Create()` — generates correlation/causation IDs for outbox message tracing

### 4.4 CQRS Commands (19 total)

#### Authentication Commands (4)

| Command | Handler | Returns | Validator |
|---------|---------|---------|-----------|
| `LoginCommand(Email, Password)` | `LoginCommandHandler` | `AuthResponse` | `LoginCommandValidator` |
| `RegisterUserCommand(FirstName, LastName, Email, Password, Role)` | `RegisterUserCommandHandler` | `AuthResponse` | `RegisterUserCommandValidator` |
| `RefreshTokenCommand(RefreshToken)` | `RefreshTokenCommandHandler` | `AuthResponse` | *(none)* |
| `ApproveOrganizerCommand(UserId)` | `ApproveOrganizerCommandHandler` | `Result` | *(none)* |

**LoginCommandHandler:** Validates email, looks up user, verifies BCrypt hash. If Organizer and not approved, returns `AuthResponse` with `RequiresApproval=true` and no token. Otherwise generates JWT + refresh token (hashed, stored via `user.IssueRefreshToken`), commits.

**RegisterUserCommandHandler:** Validates email, checks for existing user, parses Role (Attendee/Organizer only). Hashes password. Creates user. Organizers require approval (IsApproved=false). Non-organizers get JWT + refresh token immediately. Two commits (first to persist user, second for refresh token).

**RefreshTokenCommandHandler:** Hashes incoming token, looks up user by hash. If token inactive (expired/revoked), revokes all refresh tokens (security: detect token reuse/theft) and returns failure. Otherwise revokes old token, issues new refresh token, generates new access token.

**ApproveOrganizerCommandHandler:** Validates UserId, verifies role is Organizer. If already approved, returns success. Otherwise calls `user.Approve()` and commits. Controller uses `[Authorize(Roles="Admin")]`.

#### Event Commands (10)

| Command | Handler | Returns | Validator |
|---------|---------|---------|-----------|
| `CreateEventCommand(Name, Capacity, Date, Location, Description, Type, Lat?, Lng?)` | `CreateEventCommandHandler` | `Guid` | `CreateEventCommandValidator` |
| `UpdateEventCommand(EventId, Name, Capacity, Date, Location, Description)` | `UpdateEventCommandHandler` | `Result` | *(none)* |
| `PublishEventCommand(EventId)` | `PublishEventCommandHandler` | `Result` | *(none)* |
| `CancelEventCommand(EventId)` | `CancelEventCommandHandler` | `Result` | *(none)* |
| `AddTicketTypeCommand(EventId, Name, Amount, Currency, Capacity, SectionCode?)` | `AddTicketTypeCommandHandler` | `Result` | *(none)* |
| `UploadEventPhotosCommand(EventId, List<FileUploadData>)` | `UploadEventPhotosCommandHandler` | `List<EventPhotoResponse>` | `UploadEventPhotosCommandValidator` |
| `DeleteEventPhotoCommand(EventId, PhotoId)` | `DeleteEventPhotoCommandHandler` | `Result` | *(none)* |
| `ReorderEventPhotosCommand(EventId, List<Guid>)` | `ReorderEventPhotosCommandHandler` | `Result` | *(none)* |
| `SetCoverPhotoCommand(EventId, PhotoId)` | `SetCoverPhotoCommandHandler` | `Result` | *(none)* |
| `UpdatePhotoMetadataCommand(EventId, PhotoId, Caption?, DisplayOrder?)` | `UpdatePhotoMetadataCommandHandler` | `Result` | *(none)* |

**Key handler behaviors:**
- `CreateEventCommandHandler` — Creates Address (with lat/lng), creates Event via `Event.Create(...)` with UTC now from `TimeProvider`. Adds to repo, commits. Invalidates `events:list:*` cache pattern. Returns new event GUID.
- `UpdateEventCommandHandler` — Admin uses `AdminUpdateXxx()` methods; Organizer uses `UpdateXxx()` methods (different business rules per role). Commits, invalidates `event:details:{id}` and `events:list:*`.
- `UploadEventPhotosCommandHandler` — Iterates files, uploads each to `IFileStorageService`, creates `EventPhoto`, adds to event. On any failure, compensates by deleting already-uploaded files. Commits, invalidates `event:photos:{id}`.
- `SetCoverPhotoCommandHandler` — Invalidates BOTH `event:photos:{id}` (IsCover flag) AND `event:details:{id}` (CoverPhotoUrl computed field).
- `AddTicketTypeCommandHandler` — Validates section code via `IVenueLayoutValidator`. Admin uses `AdminAddTicketType`; Organizer uses `AddTicketType`.

#### Booking Commands (5)

| Command | Handler | Returns | Validator |
|---------|---------|---------|-----------|
| `CreateBookingCommand(EventId, TicketTypeId, Quantity, PaymentMethod)` | `CreateBookingCommandHandler` | `CreateBookingResponse` | `CreateBookingCommandValidator` |
| `ConfirmBookingCommand(BookingId)` | `ConfirmBookingCommandHandler` | `bool` | *(none)* |
| `ConfirmBookingFromWebhookCommand(BookingId, StripeEventId)` | `ConfirmBookingFromWebhookCommandHandler` | `bool` | *(none)* |
| `ConfirmDeferredPaymentCommand(ReferenceCode)` | `ConfirmDeferredPaymentCommandHandler` | `Result` | `ConfirmDeferredPaymentCommandValidator` |
| `CancelBookingCommand(BookingId)` | `CancelBookingCommandHandler` | `bool` | *(none)* |

**CreateBookingCommandHandler** (most complex handler — uses `IServiceScopeFactory` for scoped retry):
1. Get current user ID
2. Validate TicketTypeId and EventId value objects
3. Retry loop (max 3 attempts for `ConcurrencyException`)
4. **AttemptBooking:** creates scope, gets repos. Loads event, finds ticket type. Calls `event.ReserveSeats(...)`. Creates `Booking.Create(...)`. Adds booking to repo and commits the booking, reserved seats, and outbox messages atomically.
5. For `Instant` payment, calls `IPaymentService.InitiatePaymentAsync(..., idempotencyKey)` only after the local commit succeeds. The idempotency key uses `payment-initiate:{bookingId}`.
6. If payment initiation fails or throws after the local commit, stages a `CompensationLogDto` and commits it with `CommitWithoutEventsAsync()` so `CompensationProcessor` can retry external cleanup.
7. On success, invalidates `event:details:{id}` and returns `CreateBookingResponse(BookingId, PaymentUrl, ClientSecret)`.

**ConfirmBookingFromWebhookCommandHandler** — Idempotency-guarded. Uses deterministic GUID from `StripeEventId` (MD5 hash) as idempotency key. If already processed, returns success. If booking already Confirmed, returns success. Calls `booking.Confirm()` + `event.ConfirmReservation()`. Marks as processed (track-only, atomic with commit). Commits. Invalidates `event:details:{id}`.

**CancelBookingCommandHandler** — Admin/Organizer can cancel any booking; regular users can only cancel their own Pending/PendingPayment bookings. For Confirmed bookings, calls `event.RefundSeats()` (Sold--); for Pending/PendingPayment, calls `event.ReleaseSeats()` (Reserved--). Calls `booking.Cancel()`. Commits. Invalidates `event:details:{id}`.

### 4.5 CQRS Queries (7 total)

#### Event Queries (3)

| Query | Handler | Returns | Caching |
|-------|---------|---------|---------|
| `GetEventsQuery(Page, PageSize, Type?, UserLatitude?, UserLongitude?, DistanceInKm=20)` | `GetEventsHandler` | `PaginatedEventResponse` | Yes — 30s TTL, key: `events:list:{page}:{size}:{type}:{lat}:{lng}:{dist}` |
| `GetEventDetailsQuery(Id)` | `GetEventDetailsHandler` | `EventDetailsResponse` | Yes — 60s TTL, key: `event:details:{id}` |
| `GetEventPhotosQuery(EventId)` | `GetEventPhotosQueryHandler` | `List<EventPhotoResponse>` | Yes — 120s TTL, key: `event:photos:{eventId}` |

**GetEventsHandler** — Cache-aside pattern. Queries `IApplicationReadDbContext.Query<Event>()` filtering by future date, optional `EventType`, and a bounding box (±degrees = distanceInKm / 111.0) for geolocation. Gets total count, applies pagination + projection to `EventCardResponse` (includes lowest ticket price, cover photo URL, sold count). Applies a **Haversine post-filter** after SQL materialization to remove bounding-box corners outside the true radius. Caches the result.

#### Booking Queries (4) — All uncached (financial/personal data)

| Query | Handler | Returns |
|-------|---------|---------|
| `GetAllBookingsQuery(Status?)` | `GetAllBookingsQueryHandler` | `List<GetAllBookingsResponse>` |
| `GetBookingsByUserQuery()` | `GetBookingsByUserQueryHandler` | `List<BookingByUserResponse>` |
| `GetBookingDetailsQuery(BookingId)` | `GetBookingDetailsQueryHandler` | `GetBookingDetailsResponse` |
| `GetBookingByEventQuery(EventId)` | `GetBookingByEventQueryHandler` | `List<GetBookingByEventQueryResponse>` |

### 4.6 Domain Event Handlers (2)

#### BookingCreatedEventHandler
- **File:** `Features/Bookings/Events/BookingCreated/BookingCreatedEventHandler.cs`
- **Implements:** `IDomainEventHandler<BookingCreatedEvent>`
- **Purpose:** Marks the creation event as processed and leaves confirmation to the signed Stripe webhook or deferred-payment confirmation path.

**Flow:**
1. Null/empty BookingId guard
2. Idempotency check: `IsProcessedAsync(bookingIdValue)` — if already processed, skip
3. Load booking. If not found or already terminal, skip
4. Log that the booking is awaiting external confirmation
5. `MarkAsProcessed(bookingIdValue, "booking-created:{bookingIdValue}", utcNow)` — track-only
6. `CommitWithoutEventsAsync()` — no booking confirmation event is generated from this handler

#### BookingCancelledEventHandler
- **File:** `Features/Bookings/Events/BookingCancelled/BookingCancelledEventHandler.cs`
- **Implements:** `IDomainEventHandler<BookingCancelledEvent>`
- **Purpose:** Log-only handler. Seat release already handled by `CancelBookingCommandHandler` synchronously.

**Flow:**
1. Idempotency check
2. Log "Booking cancelled — seat release already handled by command handler"
3. `MarkAsProcessed(bookingIdValue, "booking-cancelled:{bookingIdValue}", utcNow)` — track-only
4. `CommitWithoutEventsAsync()` — no domain events to extract

#### BookingCreatedEventValidator
- **File:** `Features/Bookings/Events/BookingCreated/BookingCreatedEventValidator.cs`
- **Implements:** `IEventValidator<BookingCreatedEvent>`
- Pre-processing validation: checks booking exists and is in `Pending` status before handler runs.

### 4.7 Validators (8 FluentValidation + 1 custom)

| Validator | Validates | Key Rules |
|-----------|-----------|-----------|
| `LoginCommandValidator` | `LoginCommand` | Email: NotEmpty + EmailAddress. Password: NotEmpty. |
| `RegisterUserCommandValidator` | `RegisterUserCommand` | Email: NotEmpty, EmailAddress, MaxLength(256). Password: NotEmpty, MinLength(8), uppercase + digit required. FirstName/LastName: NotEmpty, MaxLength(100). Role: "Attendee" or "Organizer". |
| `CreateEventCommandValidator` | `CreateEventCommand` | Name: NotEmpty, MaxLength(100). Date: GreaterThan(now). Capacity: GreaterThan(0). Description: MaxLength(500). Location: NotNull, uses `AddressResponseValidator`. |
| `AddressResponseValidator` | `AddressResponse` | Custom `IPropertyValidator`: value not null, Street not whitespace, City not whitespace. |
| `UploadEventPhotosCommandValidator` | `UploadEventPhotosCommand` | EventId: NotEmpty. Photos: NotEmpty, count <= 10. Each file: length > 0, <= 5MB, content type in [image/jpeg, image/png, image/webp]. |
| `CreateBookingCommandValidator` | `CreateBookingCommand` | EventId: NotEmpty. TicketTypeId: NotEmpty. Quantity: GreaterThan(0). |
| `ConfirmDeferredPaymentCommandValidator` | `ConfirmDeferredPaymentCommand` | ReferenceCode: NotEmpty, matches `^FAW-[A-F0-9]{8}$`. |
| `BookingCreatedEventValidator` | `BookingCreatedEvent` | Custom `IEventValidator`: checks booking exists and is in an active awaiting-confirmation state. |

### 4.8 DTOs / Response Models (20+)

**Authentication:** `AuthResponse(UserId, Email, Role, Token?, ExpiresAt?, RequiresApproval, RefreshToken?)`

**Events:**
- `EventCardResponse(Id, Title, Date, City, Country, LowestPrice, Currency, Status, TotalSold, TotalCapacity, TicketTypeCount, Description?, CoverPhotoUrl?, Type, Latitude?, Longitude?)`
- `PaginatedEventResponse(Items, TotalCount, Page, PageSize, TotalPages)`
- `EventDetailsResponse(Id, Name, Date, Description, Status, Type, Capacity, LowestTicketPrice, TotalSold, Location, TicketDetails, CoverPhotoUrl)`
- `AddressResponse(Country, City, Street, Latitude?, Longitude?)`
- `TicketDetailsResponse(Id, Price, Currency, name, Capacity, SectionCode?, VenueType?)`
- `EventPhotoResponse(Id, PublicUrl, Caption?, DisplayOrder, IsCover, UploadedAt)`

**Bookings:**
- `CreateBookingResponse(BookingId, PaymentUrl?, ClientSecret?)`
- `GetAllBookingsResponse(Id, EventId, UserId, EventTitle, AttendeeName, TicketTypeName, Quantity, TotalAmount, Currency, Status, PaymentMethod, ReferenceCode?, BookingDate, HoldExpiresAt?)`
- `BookingByUserResponse(Id, EventId, EventTitle, EventDate, EventCity, TicketTypeName, Quantity, TotalAmount, Currency, Status, BookingDate, PaymentMethod, ReferenceCode?, HoldExpiresAt?)`
- `GetBookingDetailsResponse(Id, EventId, UserId, TicketTypeId, EventTitle, Quantity, BookingDate, Status, TotalAmount, Currency, PaymentMethod, ReferenceCode?, HoldExpiresAt?)`
- `GetBookingByEventQueryResponse(Id, EventId, UserId, BookingDate, Quantity, TotalAmount, Status)`

**Outbox DTOs (defined in IOutboxRepository.cs):**
- `OutboxMessageDto(Id, EventName, Domain, Payload, OccurredOnUtc, IdempotencyKey, ProcessedOnUtc?, NextRetryOnUtc?, Error?, RetryCount)`
- `OutboxFailedMessageUpdateDto(Id, Error, NewRetryCount, NextRetryOnUtc?)`
- `DeadLetterDto(Id, EventName, Domain, Payload, OccurredOnUtc, IdempotencyKey, RetryCount, FailedReason, MovedToDeadLetterAt)`

**Other:** `PaymentInitiationResult(PaymentUrl, ClientSecret?)`, `OutboxDispatchResult(IsSuccess, ProcessedIds, FailedMessages)`, `EventMetadata(CorrelationId, CausationId?, CreatedBy?)`

---

## 5. Infrastructure Layer — Persistence, Outbox, Caching, Payments, Real-Time

### 5.1 DependencyInjection Composition Root

**File:** `Infrastructure/DependencyInjection.cs`
**Method:** `AddInfrastructure(IServiceCollection, IConfiguration, IHostEnvironment)`

**Registrations:**

| Registration | Lifetime | Purpose |
|---|---|---|
| `ApplicationDbContext` | Scoped | EF Core DbContext (SQL Server, 300s command timeout, 3 retries on failure) |
| `IOutboxRepository -> OutboxRepository` | Scoped | Outbox pattern persistence |
| `IEventRepository -> EventRepository` | Scoped | Event aggregate persistence |
| `IBookingRepository -> BookingRepository` | Scoped | Booking aggregate persistence |
| `IUserRepository -> UserRepository` | Scoped | User aggregate persistence |
| `IApplicationReadDbContext -> ReadDbContextAdapter` | Scoped | Read-side optimized (AsNoTracking) |
| `IEventPhotoRepository -> EventPhotoRepository` | Scoped | Event photo persistence |
| `IFileStorageService -> LocalFileStorageService` | Scoped | Local file upload storage |
| `IOutboxMessageService -> OutboxMessageService` | Scoped | Stages domain events into outbox |
| `IEventSerializer -> EventSerializer` | Scoped | JSON serialization of domain events |
| `IUnitOfWork -> UnitOfWork` | Scoped | Transactional commit + outbox staging |
| `IOutboxDispatcher -> OutboxDispatcher` | Scoped | Dispatches outbox messages to handlers |
| `ICompensationLogRepository -> CompensationLogRepository` | Scoped | Durable payment cleanup record persistence and locking |
| `OutboxProcessor` | HostedService | Background outbox polling (every 5s) |
| `BookingExpirationJob` | HostedService | Expires pending/pending-payment bookings (every 1 min) |
| `PaymentReconciliationJob` | HostedService | Cancels orphaned Stripe sessions (every 2 min) |
| `CompensationProcessor` | HostedService | Retries durable payment cleanup records (every 10s) |
| `TimeProvider.System` | Singleton | Built-in .NET 9 time abstraction |
| `JwtSettings` | Options | Bound from `"Jwt"` config section |
| `IJwtTokenGenerator -> JwtTokenGenerator` | Scoped | JWT token creation |
| `ICurrentUserService -> CurrentUserService` | Scoped | HTTP context user extraction |
| `IPasswordHasher -> PasswordHasher` | Singleton | BCrypt (work factor 12) |
| `IEventMetadataFactory -> EventMetadataFactory` | Scoped | Correlation/causation metadata |
| `IIdempotencyStore -> IdempotencyStore` | Scoped | Idempotency tracking |
| `IVenueLayoutValidator -> VenueLayoutValidator` | Scoped | Venue section code validation |
| `StripeSettings` | Options | Bound from `"Stripe"` config |
| `HttpContextAccessor` | Singleton | Required by CurrentUserService |
| `ConnectionMultiplexer` | Singleton | Redis connection (from `ConnectionStrings:Redis`) |
| `ICacheService -> RedisCacheService` | Singleton | Redis cache-aside |
| `IWebSocketConnectionManager -> WebSocketConnectionManager` | Singleton | WebSocket connection registry |
| `IRedisPubSubBroadcaster -> RedisPubSubBroadcaster` | Singleton | Redis pub/sub for seat deltas |

**Startup Validation:**
- Throws if `ConnectionStrings:DefaultConnection` is missing
- Throws if `Jwt:Secret` is null/empty or < 32 chars (security hardening)
- Throws if `Stripe` settings section is missing
- If `Stripe:UseMock == false`, throws if `SecretKey`, `WebhookSecret`, `SuccessUrl`, or `CancelUrl` are missing
- Payment service registered as `MockPaymentGateway` (when `UseMock=true`) or `StripePaymentGateway` (otherwise)

### 5.2 DbContext

**File:** `Persistence/ApplicationDbContext.cs`

**DbSets (8):** `Bookings`, `Events`, `TicketTypes`, `OutboxMessages`, `Users`, `EventPhotos`, `ProcessedEvents`, `OutboxDeadLetters`

**Key behavior:** `OnModelCreating` calls `ApplyConfigurationsFromAssembly` — auto-discovers all `IEntityTypeConfiguration<>` in the Configuration folder. `SaveChangesAsync` delegates to base (domain event extraction is in `UnitOfWork`, not via EF Core interceptors).

### 5.3 EF Core Fluent API Configurations (8)

All configurations are in `Persistence/Configuration/` and implement `IEntityTypeConfiguration<T>`.

#### EventConfiguration
- Table: `Events`. PK: `Id` (Guid, with `EventId.Create()` value conversion)
- `EventName` owned as `OwnsOne` -> column `Name` (nvarchar(100), required, indexed)
- `Location` owned as `OwnsOne` (Address VO) -> Country, City, Street, PostalCode, Latitude (float?), Longitude (float?)
- `Type` (int, `HasConversion<int>()` for EventType enum), `Status` (int, EventStatus enum)
- `RowVersion` (rowversion — optimistic concurrency)
- Indexes: `IX_Events_Date`, `IX_Events_Status`, `IX_Events_Date_Status`, `IX_Events_CreatedAt`

#### BookingConfiguration
- Table: `Bookings`. PK: `Id` (Guid, `BookingId.FromDatabase()` conversion)
- `Money` owned as `OwnsOne` -> `Amount` (decimal(18,2)), `Currency` (nvarchar(3))
- `Status` stored as string via `HasConversion<string>()`
- `PaymentMethod` stored as string
- `ReferenceCode` — unique filtered index `IX_Bookings_ReferenceCode` with filter `[ReferenceCode] IS NOT NULL`
- `RowVersion` (rowversion — optimistic concurrency)
- Indexes: `IX_Bookings_UserId`, `IX_Bookings_EventId`, `IX_Bookings_TicketTypeId`, `IX_Bookings_Status`, `IX_Bookings_BookingDate`, composite indexes

#### TicketTypeConfiguration
- Table: `TicketTypes`. PK: `Id` (Guid, `TicketTypeId.Create()` conversion)
- FK to Events with `DeleteBehavior.Cascade`
- `Price` owned as `OwnsOne` (Money) -> `Price` (decimal(18,2)), `Currency` (nvarchar(3))
- Ignored computed properties: `AvailableCount`, `OccupancyRate`, `ReservationRate`, `UnavailableCount` (not persisted — derived)
- Indexes: `IX_TicketTypes_EventId`, `IX_TicketTypes_Name`, `UX_TicketTypes_EventId_Name` (unique), `IX_TicketTypes_EventId_Capacity_SoldCount_ReservedCount` (flash-sale availability checks)
- `RowVersion` (rowversion)

#### UserConfiguration
- Table: `Users`. PK: `Id` (Guid, `UserId.Create()` conversion)
- `Email` owned as `OwnsOne` -> column `Email` (nvarchar(256), unique index)
- `Role` stored as string via `Role.FromString()` conversion
- `RefreshTokens` owned as `OwnsMany` -> table `RefreshTokens` (PK: Id int, FK: UserId, unique TokenHash index)
- `RowVersion` (rowversion)

#### Other Configurations
- `EventPhotoConfiguration` — table `EventPhotos`, FK cascade to Events, `IX_EventPhotos_EventId_IsCover`, `IX_EventPhotos_DisplayOrder`
- `OutboxConfiguration` — table `OutboxMessages`, unique `IdempotencyKey` index, 5 indexes including `IX_OutboxMessages_ReadyForProcessing`
- `OutboxDeadLetterConfiguration` — table `OutboxDeadLetters`, `FailedReason` (nvarchar(4000))
- `ProcessedEventConfiguration` — table `ProcessedEvents`, PK: EventId, unique IdempotencyKey

### 5.4 Unit of Work — Transactional Outbox Integration

**File:** `Persistence/UnitOfWork.cs`

**`CommitAsync(CancellationToken)`:**
1. `ExtractDomainEvents()` — iterates `ChangeTracker.Entries<IAggregateRoot>()`, collects all `DomainEvents` from each aggregate, then calls `ClearDomainEvents()` on each
2. If events exist, calls `_outboxService.AddFromDomainEvents(domainEvents)` — stages outbox messages in the same DbContext transaction
3. Calls `_context.SaveChangesAsync()` — persists everything atomically (entities + outbox messages in one transaction)
4. On `DbUpdateConcurrencyException`, re-throws as `ConcurrencyException`

**`CommitWithoutEventsAsync(CancellationToken)`:** Direct `SaveChangesAsync` — no domain event extraction. Used by handlers that only need to persist an idempotency marker (e.g., `BookingCancelledEventHandler`).

**Key design:** Domain events are extracted from aggregates BEFORE `SaveChangesAsync`, staged into the outbox table, and both are persisted in the SAME database transaction. This is the **transactional outbox pattern** — guarantees no events are lost if the commit succeeds.

**No EF Core Interceptors:** Domain event extraction is explicit in the UnitOfWork, not hidden in a `SaveChangesInterceptor`. This is a deliberate design choice for clarity.

### 5.5 Repository Implementations (6)

All repositories are in `Persistence/Repositories/`.

| Repository | File | Key Methods | Concurrency Strategy |
|-----------|------|-------------|---------------------|
| `EventRepository` | `EventRepository.cs` | `AddEventAsync`, `GetByIdAsync` (includes TicketTypes + Photos) | Row version (optimistic) |
| `BookingRepository` | `BookingRepository.cs` | `AddBookingAsync`, `GetByIdAsync`, `GetExpiredPendingBookingsAsync`, `GetPendingInstantBookingsPastHoldAsync`, `GetByReferenceCodeAsync` | Raw SQL `UPDLOCK, READPAST, ROWLOCK` for batch dequeue |
| `UserRepository` | `UserRepository.cs` | `GetByEmailAsync`, `GetByIdAsync`, `GetByRefreshTokenHashAsync`, `AddAsync` (all include RefreshTokens) | Row version |
| `EventPhotoRepository` | `EventPhotoRepository.cs` | `GetByEventIdAsync`, `GetByIdAsync`, `Add`, `Update`, `Delete` | Sync tracked ops |
| `OutboxRepository` | `OutboxRepository.cs` | `Add`, `AddRange`, `GetAndLockUnprocessedMessagesAsync`, `MarkRangeAsProcessedAsync`, `MarkRangeAsFailedAsync`, `ReleaseLockAsync`, `GetUnprocessedCountAsync`, `MoveToDeadLetterAsync`, `GetDeadLettersAsync`, `RequeueDeadLetterAsync` | Raw SQL `UPDATE TOP(n) WITH (UPDLOCK, READPAST, ROWLOCK) SET ProcessingLock=... OUTPUT INSERTED.*` — atomic lock-and-fetch with 5-min stale-lock reclaim |
| `IdempotencyStore` | `IdempotencyStore.cs` | `IsProcessedAsync`, `MarkAsProcessedAsync` (sync save), `MarkAsProcessed` (track-only) | Unique index on IdempotencyKey |

### 5.6 Outbox Pattern Implementation

#### OutboxMessage (Entity)
- **File:** `Persistence/Outbox/OutboxMessage.cs`
- Fields: `Id`, `EventName`, `Domain`, `Payload`, `OccurredOnUtc`, `ProcessedOnUtc`, `Error`, `RetryCount`, `NextRetryOnUtc`, `IdempotencyKey`, `ProcessingLock`, `ProcessingLockedAt`
- Logic: `IsProcessed`, `IsReadyForProcessing(DateTime)`, `IsLocked(DateTime)`, `TryAcquireLock(Guid, DateTime)`, `ReleaseLock(Guid)`, `MarkAsProcessed(DateTime)`, `MarkAsFailed(string error, DateTime, DateTime? nextRetry)`

#### OutboxDeadLetter (Entity)
- **File:** `Persistence/Outbox/OutboxDeadLetter.cs`
- All `init`-only properties: `Id`, `EventName`, `Domain`, `Payload`, `OccurredOnUtc`, `IdempotencyKey`, `RetryCount`, `FailedReason`, `MovedToDeadLetterAt`

#### OutboxMessageService
- **File:** `Persistence/Outbox/OutboxMessageService.cs`
- `AddFromDomainEvents(IEnumerable<IDomainEvent>)` — for each event: generates GUID, serializes via `IEventSerializer`, computes idempotency key, creates `OutboxMessageDto`, calls `_outboxRepository.AddRange(messages)`
- **Idempotency key computation:** If correlation ID exists: `"{EventName}_{CorrelationId}_{MessageId:N}"` (truncated to 100 chars). Otherwise: SHA-256 hash of `"{EventName}_{OccurredOnUtc:O}_{MessageId:N}_{Payload}"`, first 32 hex chars.

#### EventSerializer
- **File:** `Persistence/Outbox/EventSerializer.cs`
- Uses `System.Text.Json` with camelCase naming. Static constructor scans the Domain assembly for all `IDomainEvent` implementations and builds dictionaries for type lookup.
- Domain extraction by namespace: `BookingAggregate` -> "Booking", `EventAggregate` -> "Event", `UserAggregate` -> "User", else "Unknown".
- `Serialize<T>` uses runtime type. `Deserialize(eventName, payload)` looks up type by name, deserializes, returns `Result<IDomainEvent>`.

#### Value Object JSON Converters
- `ValueObjectJsonConverter<T>` — reflective converter for value objects with `Guid Value` property and `static FromDatabase(Guid)` method
- `ValueObjectJsonConverterFactory` — factory that checks if a type is a non-abstract subclass of `ValueObjectBase` with readable `Value` (Guid) property

#### OutboxProcessor (Background Service)
- **File:** `BackgroundJobs/OutboxProcessor.cs`
- Polls every **5 seconds**, batch size **50**, 15-second startup delay
- Uses unique `_lockId = Guid.NewGuid()` per processor instance
- Each iteration: creates DI scope, gets `IOutboxRepository`, `IOutboxDispatcher`, `TimeProvider`. Calls `GetAndLockUnprocessedMessagesAsync` then `DispatchBatchAsync`.
- On shutdown: releases all locks

#### OutboxDispatcher
- **File:** `BackgroundJobs/OutboxDispatcher.cs`
- `DispatchBatchAsync` — iterates each message:
  1. Deserializes payload via `IEventSerializer.Deserialize(eventName, payload)`
  2. If deserialization fails with `EventTypeNotFound`, `JsonDeserializationFailed`, or `DeserializationReturnedNull` -> **non-retryable** (dead-letter immediately)
  3. Resolves `IDomainEventHandler<T>` via `serviceProvider.GetService(typeof(IDomainEventHandler<>).MakeGenericType(eventType))` — **reflection-based handler resolution** (not MediatR)
  4. Invokes `HandleAsync` via reflection
  5. If no handler registered -> logs and returns success (no-op)
  6. If success -> adds to `processedIds`
  7. If failure: non-retryable OR retry count >= 3 -> `MoveToDeadLetterAsync`; else computes next retry (5s, 1min, 5min)
  8. After all messages: `SaveChangesAsync` (persists dead-letter moves), then `MarkRangeAsProcessedAsync` and `MarkRangeAsFailedAsync`

**Retry schedule:** Attempt 1 -> 5s, Attempt 2 -> 1min, Attempt 3 -> dead letter

### 5.7 Caching Implementation (Redis)

**File:** `Caching/RedisCacheService.cs`

- Registered as **Singleton** (shares singleton `ConnectionMultiplexer`)
- All keys prefixed with `"cache:"` for namespace isolation
- Uses `System.Text.Json` with camelCase naming
- **Graceful degradation:** All methods wrap Redis operations in try/catch. Redis failures never crash the application — they log warnings and return null/skip.

**Methods:**
- `GetAsync<T>` — `StringGetAsync`, deserialize. On exception -> returns null (graceful)
- `SetAsync<T>` — serialize, `StringSetAsync` with TTL. On exception -> swallows
- `RemoveAsync` — `KeyDeleteAsync`. Graceful on error
- `RemoveByPatternAsync` — `_server.KeysAsync(pattern: "cache:{pattern}")` to SCAN, then `KeyDeleteAsync(keys.ToArray())` in bulk
- `ClearAllAsync` — `FlushDatabaseAsync()`

**Cache coverage:**
- Event list (30s TTL, key: `events:list:{page}:{size}:{type}:{lat}:{lng}:{dist}`)
- Event details (60s TTL, key: `event:details:{id}`)
- Event photos (120s TTL, key: `event:photos:{eventId}`)
- Booking queries — intentionally uncached (financial/personal data, high mutation rate)

**Cache invalidation (all runs AFTER successful `CommitAsync`):**
- CreateEvent -> `RemoveByPatternAsync("events:*")`
- UpdateEvent -> evict `event:details:{id}` + `events:list:*`
- CreateBooking -> evict `event:details:{id}` (refresh seats capacity, prevent overbooking)
- UploadEventPhotos / DeleteEventPhoto / ReorderEventPhotos / UpdatePhotoMetadata -> evict `event:photos:{eventId}`
- SetCoverPhoto -> evict BOTH `event:photos:{eventId}` (IsCover flag) + `event:details:{eventId}` (computed CoverPhotoUrl)

### 5.8 Payment Gateway Implementations

#### StripePaymentGateway
- **File:** `Payments/StripePaymentGateway.cs`
- `InitiatePaymentAsync` — creates Stripe Checkout Session with card payment, single line item, unit amount in cents, success/cancel URLs, bookingId in metadata, and provider idempotency key
- `CancelPaymentAsync` — lists recent sessions (limit 10), finds matching bookingId metadata. If found and active, calls `ExpireAsync`. If not found or already complete/expired -> no-op success.

#### MockPaymentGateway
- **File:** `Payments/MockPaymentGateway.cs`
- `InitiatePaymentAsync` — accepts the same idempotency key and returns fake `mock://payment/{bookingId}` URL
- `CancelPaymentAsync` — logs and returns success

### 5.9 Durable Payment Compensation

#### CompensationLog
- **File:** `Persistence/Outbox/CompensationLog.cs`
- Durable record for external payment cleanup when Stripe initiation fails after a local booking commit.
- Fields include `BookingId`, `CompensationType`, `Payload`, `OccurredOnUtc`, `IdempotencyKey`, `ProcessedOnUtc?`, `Error?`, `RetryCount`, `NextRetryOnUtc?`, `ProcessingLock?`, and `ProcessingLockedAt?`.
- Uses a unique `IdempotencyKey` index to avoid duplicate compensation records.

#### CompensationLogRepository
- **File:** `Persistence/Repositories/CompensationLogRepository.cs`
- Uses `UPDATE TOP(n) ... WITH (UPDLOCK, READPAST, ROWLOCK) ... OUTPUT INSERTED.*` to atomically lock and fetch ready records.
- Supports batch processed/failed marking, stale lock release, and moving exhausted records to `OutboxDeadLetters`.
- Lock release uses the explicit `ExecuteSqlRawAsync(..., parameters: ..., cancellationToken: ...)` overload so the `CancellationToken` is not interpreted as a SQL parameter.

#### CompensationProcessor
- **File:** `BackgroundJobs/CompensationProcessor.cs`
- Polls every 10 seconds with batch size 50 after a startup delay.
- Executes `CancelPaymentAsync` for payment-cancellation compensation records.
- Retry backoff: 5s -> 30s -> 1m -> 5m -> 15m; after 5 failed attempts, moves the record to dead letter.
- Releases owned locks on graceful shutdown and relies on a 5-minute stale-lock timeout for crash recovery.

### 5.10 Real-Time Infrastructure

#### WebSocketConnectionManager
- **File:** `RealTime/WebSocketConnectionManager.cs`
- In-memory `ConcurrentDictionary` connection registry
- `Add(WebSocket, eventId, userId)` — creates connection ID, stores `ConnectionEntry`, adds to event-group map
- `GetConnections(eventId)` — returns all open WebSockets for an event
- `BroadcastToEventAsync(eventId, message, ct)` — sends UTF-8 bytes to all sockets; removes dead connections on failure
- `SendToConnectionAsync(connectionId, message, ct)` — sends to single connection

#### RedisPubSubBroadcaster
- **File:** `RealTime/RedisPubSubBroadcaster.cs`
- Channel pattern: `seats:event:{eventId}`
- `SubscribeToEventAsync(eventId, onMessage, ct)` — subscribes to Redis channel, prevents double-subscription
- `PublishAsync(eventId, message)` — publishes to channel

#### SeatStateDelta
- **File:** `RealTime/SeatStateDelta.cs`
- DTO: `Type` ("DELTA"/"COLLISION"), `SeatId`, `Status`, `Ts`
- Factory methods: `Delta(seatId, status, ts)`, `Collision(seatId, reason, ts)`

### 5.11 Background Jobs (4)

| Job | File | Interval | Batch Size | Purpose |
|-----|------|----------|-----------|---------|
| `OutboxProcessor` | `BackgroundJobs/OutboxProcessor.cs` | 5s | 50 | Polls outbox table, dispatches to handlers |
| `BookingExpirationJob` | `BackgroundJobs/BookingExpirationJob.cs` | 1 min | 100 | Expires pending/pending-payment bookings past hold, releases seats |
| `PaymentReconciliationJob` | `BackgroundJobs/PaymentReconciliationJob.cs` | 2 min | 50 | Cancels orphaned Stripe checkout sessions for expired Instant bookings |
| `CompensationProcessor` | `BackgroundJobs/CompensationProcessor.cs` | 10s | 50 | Retries durable external cleanup records and dead-letters exhausted entries |

**BookingExpirationJob** — Calls `GetExpiredPendingBookingsAsync` for `Pending` and `PendingPayment` bookings (raw SQL with `UPDLOCK, READPAST, ROWLOCK`). For each expired booking: `booking.Expire(now)` + `evt.ReleaseSeats(...)`. Commits (stages any domain events to outbox).

**PaymentReconciliationJob** — Calls `GetPendingInstantBookingsPastHoldAsync` (AsNoTracking, read-only). For each: calls `paymentService.CancelPaymentAsync`. Log-only on failure (does not modify booking state).

### 5.12 Authentication Infrastructure

| Component | File | Purpose |
|-----------|------|---------|
| `JwtSettings` | `Authentication/JwtSettings.cs` | Config: Secret, Issuer, Audience, ExpiryMinutes (1440), RefreshTokenExpiryDays (7) |
| `JwtTokenGenerator` | `Authentication/JwtTokenGenerator.cs` | Generates JWT with claims (NameIdentifier, Email, Role, Jti). Signs HmacSha256. Also generates/hashes refresh tokens. |
| `CurrentUserService` | `Authentication/CurrentUserService.cs` | Extracts user ID and role from `HttpContext.User` claims |
| `PasswordHasher` | `Authentication/PasswordHasher.cs` | BCrypt with work factor 12 |

### 5.13 Other Infrastructure

| Component | File | Purpose |
|-----------|------|---------|
| `EventMetadataFactory` | `Messaging/EventMetadataFactory.cs` | Creates `EventMetadata` with correlation ID (from HttpContext items or new Guid), causation ID, and user ID |
| `VenueLayoutValidator` | `Services/VenueLayoutValidator.cs` | Maps `EventType` -> valid section codes (Sports: 10 zones, Music: 9 zones, Theater: 8 zones) |
| `LocalFileStorageService` | `Storage/LocalFileStorageService.cs` | Max 5MB, [jpeg/png/webp], base path `wwwroot/uploads/events/`, unique filenames `{Guid:N}_{safeFileName}` |
| `DatabaseSeeder` | `Persistence/SeedData/DatabaseSeeder.cs` | Seeds 12 users (1 admin, 2 organizers, 10 attendees), 18 events across 8 EventTypes with real coordinates, venue-mapped ticket types, ~75 bookings (75% Instant, 25% Deferred) |
| `DatabaseSeederService` | `Persistence/SeedData/DatabaseSeederService.cs` | `IHostedService` — runs `Database.MigrateAsync` then `SeedDataAsync` on startup |

---

## 6. WebApi Presentation Layer — Controllers, Middleware, WebSocket Gateway

### 6.1 Program.cs (Startup / Entry Point)

**Service Registration (in order):**
1. **CORS** — Policy `"AllowAngular"`: dev origins (`localhost:4200`, `127.0.0.1:4200`, `localhost:57354`, `127.0.0.1:57354`), prod from config. `AllowAnyHeader`, `AllowAnyMethod`, `AllowCredentials`
2. **Controllers** with JSON options: CamelCase, `DefaultIgnoreCondition=WhenWritingNull`, `JsonStringEnumConverter`, custom `ValueObjectJsonConverterFactory`
3. **OpenAPI** — `AddOpenApi()`
4. **Application** — `AddApplication()`
5. **Infrastructure** — `AddInfrastructure(config, environment)`
6. **DatabaseSeederService** — `AddHostedService`
7. **FormOptions** — `MultipartBodyLengthLimit = 60MB`

**Middleware Pipeline (in order):**
1. `CorrelationIdMiddleware` (first — every request gets correlation ID)
2. `UseWebSockets()` with `KeepAliveInterval = 30s`
3. `WebSocketMiddleware` (intercepts `/ws/venues/{eventId}`)
4. `GlobalExceptionHandlingMiddleware` (catch-all exception handler)
5. `UseStaticFiles()` (serves `wwwroot/` — uploaded photos)
6. `UseHttpsRedirection()`
7. `UseCors("AllowAngular")`
8. `UseAuthorization()`
9. `MapControllers()`

### 6.2 Controllers (7, ~35 endpoints)

#### AuthController — `api/auth`
- `POST /register` — [AllowAnonymous] — Registers user, sets refresh token as HttpOnly cookie (Secure, SameSite=Strict)
- `POST /login` — [AllowAnonymous] — Authenticates, sets refresh token cookie
- `POST /refresh` — [AllowAnonymous] — Reads refresh token from cookie, issues new tokens
- `POST /revoke` — [Authorize] — Deletes refresh token cookie
- `POST /organizers/{userId}/approve` — [Authorize(Roles="Admin")] — Approves organizer

#### EventController — `api/event`
- `GET /{id}` — [AllowAnonymous] — Event details by ID
- `GET /` — [AllowAnonymous] — Paginated event listing with filters (page, pageSize, type, userLatitude, userLongitude, distanceInKm)
- `POST /` — [Authorize(Roles="Organizer,Admin")] — Create event (returns CreatedAtRoute)
- `PUT /{id}` — [Authorize(Roles="Organizer,Admin")] — Update event (enforces route-body ID match)
- `POST /{eventId}/ticket-types` — [Authorize(Roles="Organizer,Admin")] — Add ticket type
- `PUT /{id}/cancel` — [Authorize(Roles="Organizer,Admin")] — Cancel event (PUT, not DELETE — BUG #15 fix)
- `GET /{id}/photos` — [AllowAnonymous] — Get all photos
- `POST /{id}/photos` — [Authorize(Roles="Organizer,Admin")] — Upload photos (multipart/form-data)
- `DELETE /{id}/photos/{photoId}` — [Authorize(Roles="Organizer,Admin")] — Delete photo
- `PUT /{id}/photos/{photoId}/cover` — [Authorize(Roles="Organizer,Admin")] — Set cover photo
- `PUT /{id}/photos/{photoId}/metadata` — [Authorize(Roles="Organizer,Admin")] — Update caption/display order
- `PUT /{id}/photos/reorder` — [Authorize(Roles="Organizer,Admin")] — Reorder photos

#### BookingController — `api/booking` — [Authorize] (all endpoints)
- `GET /{id}` — Booking details
- `GET /event/{eventId}` — Bookings for an event
- `POST /` — Create booking (returns CreatedAtRoute)
- `POST /{bookingId}/confirm` — Confirm booking
- `PUT /{id}/cancel` — Cancel booking
- `GET /my` — Current user's bookings
- `POST /confirm-deferred` — Confirm deferred payment

#### AdminController — `api/admin/outbox` — [Authorize(Roles="Admin")]
- `GET /dead-letters` — List all dead-letter messages
- `POST /dead-letters/{id}/requeue` — Requeue dead-letter message

#### AdminEventController — `api/admin/events` — [Authorize(Roles="Admin")]
- `PUT /{id}` — Admin update event
- `POST /{eventId}/ticket-types` — Admin add ticket type
- `POST /{eventId}/publish` — Admin publish event

#### AdminBookingController — `api/admin/bookings` — [Authorize(Roles="Admin")]
- `GET /` — All bookings (optional status filter)
- `POST /{bookingId}/confirm` — Admin confirm booking
- `PUT /{bookingId}/cancel` — Admin cancel booking

#### StripeWebhookController — `api/webhooks/stripe` — No auth attributes (Stripe signature verification)
- `POST /` — Reads raw body, verifies Stripe signature via `EventUtility.ConstructEvent`. Handles `checkout.session.completed` events only. Extracts bookingId from session metadata, checks `PaymentStatus == "paid"`, dispatches `ConfirmBookingFromWebhookCommand`.

### 6.3 Middleware (3)

#### CorrelationIdMiddleware
- **File:** `Middlewares/CorrelationIdMiddleware.cs`
- Reads `X-Correlation-Id` header; if absent, generates new `Guid`
- Stores in `HttpContext.Items["CorrelationId"]`
- Adds to response headers as `X-Correlation-Id`
- Registered first in pipeline (before all others)

#### GlobalExceptionHandlingMiddleware
- **File:** `Middlewares/GlobalExceptionHandlingMiddleware.cs`
- Centralized exception-to-ProblemDetails converter

| Exception | HTTP Status | Error Code | Detail |
|-----------|-------------|------------|--------|
| `ConcurrencyException` | 409 Conflict | `concurrency.conflict` | "The resource was modified by another request. Please retry." |
| `DbUpdateException` | 500 | `database.error` | "A database error occurred." |
| `OperationCanceledException` | 499 | — | No body (client closed request) |
| `UnauthorizedAccessException` | 403 Forbidden | `unauthorized` | "You do not have permission..." |
| `Exception` (catch-all) | 500 | `server.error` | "An unexpected error occurred." |

Output: `application/problem+json` with `errorCode` extension field, CamelCase JSON.

#### WebSocketMiddleware (340 lines — largest file)
- **File:** `Middlewares/WebSocketMiddleware.cs`
- Intercepts requests starting with `/ws/venues` that are WebSocket upgrades
- Extracts JWT from `?token=` query parameter, decodes claims (extracts `sub` as userId)
- Path parsing: extracts `eventId` from `/ws/venues/{eventId}`
- **Connection lifecycle:**
  1. Accepts WebSocket, registers with `IWebSocketConnectionManager`
  2. Sends `CONNECTED` ack with connectionId
  3. Subscribes to Redis Pub/Sub for the event
  4. Starts heartbeat task (PING every 15s; if no PONG within 10s, removes connection)
  5. Enters receive loop
- **Message handling:** `PONG` -> updates timestamp; `LOCK` -> delegates to `HandleLockAsync`; `UNLOCK` -> best-effort
- **HandleLockAsync (seat locking flow):** Extracts `seatId` and `ticketTypeId`, creates DI scope, loads Event with TicketTypes, calls `event.ReserveSeats(ticketTypeId, 1, utcNow)`. On success: `SaveChangesAsync`, broadcasts `DELTA` via Redis Pub/Sub, sends `ACK` to locker. On failure: sends `COLLISION`. On `DbUpdateConcurrencyException`: sends `COLLISION` with "RESERVED_BY_OTHER".

### 6.4 ResultExtensions

**File:** `Extensions/ResultExtensions.cs`

Converts domain `Result`/`Result<T>` to ASP.NET Core `IActionResult`:
- Success -> `OkResult` or `OkObjectResult(value)` or `CreatedAtRouteResult`
- Failure -> picks worst error type via `MaxBy(e => e.Type)` and maps: `Validation->400`, `NotFound->404`, `Conflict->409`, `Unauthorized->401`, default->`500`
- All failures produce `ProblemDetails` with `errors` array extension containing `{ Code, Message, Type }`

---

## 7. Angular Frontend — Standalone Components, Signals, D3 Seating Engine

### 7.1 Technology Stack
- **Angular 19.1** — standalone-first, signals-native, OnPush change detection by default
- **D3.js 7.9** — SVG venue rendering with surgical DOM updates (bypasses Angular change detection for performance)
- **CDK 19.0** — drag-drop for photo reordering
- **Tailwind CSS 3.4** — utility-first styling
- **RxJS 7.8** — used in seating chart services (Subjects/Observables for WebSocket events)
- **No NgRx** — signals used exclusively for state management

### 7.2 Architecture — 4-Layer Frontend Layout

```
core/           -> Models, enums, mappers, guards (pure TS, minimal Angular deps)
application/    -> HTTP services + application services (orchestration, caching, mapping, signals)
infrastructure/ -> Interceptors, directives, storage, toast (cross-cutting concerns)
shared/         -> 8 reusable components + 1 pipe
features/       -> 15 feature components (lazy-loaded via loadComponent)
```

### 7.3 HTTP Services (7, all extend `HttpClientBase`)

| Service | Base URL | Key Methods |
|---------|----------|-------------|
| `EventHttpService` | `/api/event` | `getEvents(query)`, `getEvent(id)`, `createEvent(data)`, `cancelEvent(id)`, `updateEvent(id, data)`, `addTicketType(eventId, data)` |
| `EventPhotoHttpService` | `/api/event` | `getPhotos(eventId)`, `uploadPhotos(eventId, files)`, `deletePhoto(eventId, photoId)`, `setCoverPhoto(eventId, photoId)`, `updatePhotoMetadata(eventId, photoId, data)`, `reorderPhotos(eventId, orderedIds)` |
| `BookingHttpService` | `/api/booking` | `createBooking(data)`, `getBooking(id)`, `getBookingsByEvent(eventId)`, `getMyBookings()`, `confirmBooking(id)`, `cancelBooking(id)`, `confirmDeferredPayment(data)` |
| `AuthHttpService` | `/api/auth` | `login(credentials)`, `register(data)` |
| `AdminOutboxHttpService` | `/api/admin/outbox` | `getDeadLetters()`, `requeueDeadLetter(id)` |
| `AdminEventHttpService` | `/api/admin/events` | `updateEvent(id, data)`, `addTicketType(eventId, data)`, `publishEvent(eventId)` |

**HttpClientBase** — abstract base providing `HttpClient`, `apiUrl`, and `toErrorResult()` which converts `HttpErrorResponse` into `Result<never>` (handles ProblemDetails, validation errors, status-based messages).

### 7.4 Application Services (4, orchestration with signals/caching)

| Service | Key Signals | Purpose |
|---------|-------------|---------|
| `EventApplicationService` | `userLocation`, `nearbyEnabled` | Event facade with 30s TTL Map-based cache-aside (`shareReplay`), geolocation, photo operations |
| `BookingApplicationService` | — | Booking facade, maps backend DTOs to frontend models via `bookingByUserToBooking` |
| `AuthApplicationService` | `currentUser`, `isAuthenticated` (computed), `userRole` (computed) | Auth state manager, persists to `sessionStorage` (key `auth_user`) |
| `SeatingChartService` | — | Resolves venue seating config from `Event.type` -> `VenueEventType`. Builds SVG zone geometry for Sport/Concert/Theater. Caches by event ID. |

### 7.5 Interceptors (2, functional)

| Interceptor | File | Purpose |
|-------------|------|---------|
| `authInterceptor` | `infrastructure/interceptors/auth.interceptor.ts` | Attaches `Authorization: Bearer {token}` to every outgoing request except `/api/auth/login` and `/api/auth/register`. Token from `AuthApplicationService.getToken()`. |
| `errorInterceptor` | `infrastructure/interceptors/error.interceptor.ts` | Global HTTP error handler. Maps status codes to toast messages: 400 (validation), 401 (session expired -> logout), 403 (-> navigate to /unauthorized), 404, 409, 500+, 0 (network). Re-throws after toasting. |

### 7.6 Route Guards (2)

| Guard | File | Purpose |
|-------|------|---------|
| `authGuard` | `core/guards/auth.guard.ts` | `CanActivateFn` — allows when `isAuthenticated()` returns true, else redirects to `/login` with `returnUrl` |
| `roleGuard` | `core/guards/role.guard.ts` | Factory returning `CanActivateFn` — `roleGuard(allowedRoles: UserRole[])` checks user role, redirects to `/unauthorized` if not allowed |

**Route usage:**
- `events/create` -> authGuard + roleGuard(['Organizer', 'Admin'])
- `events/:id/edit` -> authGuard + roleGuard(['Admin', 'Organizer'])
- `dashboard/organizer` -> authGuard + roleGuard(['Organizer', 'Admin'])
- `dashboard/attendee` -> authGuard + roleGuard(['Attendee'])
- `bookings/:id` -> authGuard
- `admin/outbox/dead-letters` -> authGuard + roleGuard(['Admin'])

### 7.7 D3 Seating Chart Engine

The seating chart is the most complex frontend component, using D3.js for direct SVG manipulation with a signal-based state store:

#### VenueViewStore (Signal Store)
- **State signals:** `mode` ('SPORT'|'CONCERT'), `venueData`, `selectedSeatIds`, `filter`
- **Computed views:** `visibleSeats`, `blockSummaries`, `totalSelected`, `selectedSeatsMetadata`, `totalCartPrice`, `totalPrice`
- **Mutators:** `loadVenue()`, `setMode()`, `toggleSeat()`, `isSelected()`, `clearSelection()`, `resetForModeSwitch()`, `applyServerDelta()`, `setFilter()`

#### VenueGraphRendererService (D3 Pipeline)
- `initializeEngine()`, `renderVenue()`, `purgeGraph()`, `applyStatusDelta()` (surgical per-node), `flashCollision()`, `updateSeatVisualState()`, `highlightBlock()`, `resetZoom()`, `getCurrentScale()`
- Owns SVG lifecycle, zoom/pan, full re-render + surgical updates

#### LiveStateSyncService (WebSocket Real-Time)
- Connects to `/ws/venues/{venueId}?token=...`
- Handles DELTA, COLLISION, PING, ACK, CONNECTED messages
- Auto-reconnect with exponential backoff
- 25s heartbeat
- Signals: `status`, `lastDeltaAt`, `reconnectAttempts`, `connectionId`
- Observables: `deltas$`, `collisions$`, `acks$`

#### SeatLockOrchestratorService
- Wires user clicks to WebSocket LOCK/UNLOCK
- Handles collision rollback
- `pendingLocks` signal (readonly) — seats with in-flight LOCK messages
- `acquire(seatId, price)` — sends LOCK, optimistic UI update; short-circuits if seat already selected
- `release(seatId)` — sends UNLOCK

#### SeatingChartComponent (Orchestrator)
- Wires store + renderer + live sync + lock orchestrator
- `executeFinalBooking()` — bypasses `acquire()` short-circuit by sending LOCK directly via `liveSync.send()` for all selected seats
- `submitting` signal — guards `handleSeatClick` during in-flight, drives `.is-loading` CSS class on floating pill
- Floating pill — moved outside `.sc-canvas` to avoid `overflow: hidden` clipping; flex-flow natural positioning
- Calls `POST /api/booking` via `BookingApplicationService.createBooking()` with `{ eventId, ticketTypeId, quantity: selectedSeats.length, paymentMethod }`
- Navigates to `/bookings/{bookingId}` on success
- `bookingTotal` computed signal — derives from `eventData.ticketTypes[ticketTypeId].price * store.totalSelected()` (matches backend formula)

### 7.8 Signal-Based State Management Summary

The project uses Angular Signals exclusively — no NgRx. Key signal containers:
- `AuthApplicationService` — `currentUser`, `isAuthenticated` (computed), `userRole` (computed)
- `EventApplicationService` — `userLocation`, `nearbyEnabled`, private Map cache (30s TTL)
- `ToastService` — `toasts` signal
- `VenueViewStore` — full signal store (state + computed + mutators)
- `SeatLockOrchestratorService` — `pendingLocks` signal
- `LiveStateSyncService` — `status`, `lastDeltaAt`, `reconnectAttempts`, `connectionId`
- Per-component signals in 15+ components (loading, submitting, filters, etc.)

---

## 8. Testing Architecture — 3-Tier Pyramid with Concurrency Engine

### 8.1 Test Pyramid

```
                    Concurrency Tests (5 classes, ~10 tests)
                   /  Real SQL Server, concurrent HTTP, race conditions
                  /   Testcontainers + ConcurrentExecutor (async barrier)
                 /
    Integration Tests (10 classes, ~34 tests)
   /  Full HTTP pipeline, real DB, fakes for Redis/Payments
  /   Testcontainers + Respawn + WebApplicationFactory
 /
Application Unit Tests (5 classes, ~31 tests)
  Mocked repos via NSubstitute, TimeProvider.System
/
Domain Unit Tests (8 classes, ~74 tests)
   Pure domain logic, no mocks, no infrastructure
```

**Total:** ~149 test methods across 44 C# files in 5 test projects.

### 8.2 Testing Libraries

| Library | Version | Purpose |
|---------|---------|---------|
| xUnit | 2.9.2 | Test framework |
| FluentAssertions | 7.0.0 | Assertion library |
| NSubstitute | 5.3.0 | Mocking (Application unit tests) |
| Microsoft.AspNetCore.Mvc.Testing | 9.0.0 | WebApplicationFactory |
| Testcontainers.MsSql | 4.0.0 | Real SQL Server 2022 in Docker |
| Respawn | 6.0.0 | Database reset between tests |
| WireMock.Net | 2.12.0 | HTTP mock (referenced, not yet used) |

### 8.3 Testing Foundation (Shared Library)

| Component | File | Purpose |
|-----------|------|---------|
| `EventyWebApplicationFactory` | `Web/EventyWebApplicationFactory.cs` | Spins up SQL Server container, replaces Redis/Payments with fakes, removes background services, adds TestAuthenticationHandler |
| `TestAuthenticationHandler` | `Web/TestAuthenticationHandler.cs` | Reads `X-Test-UserId` and `X-Test-Role` headers — no real JWT needed |
| `SqlServerContainerFactory` | `Containers/SqlServerContainerFactory.cs` | `mcr.microsoft.com/mssql/server:2022-latest` |
| `DatabaseResetService` | `Database/DatabaseResetService.cs` | Respawn — wipes data, keeps schema |
| `FakePaymentService` | `Fakes/FakePaymentService.cs` | Payment failure toggle, idempotency key tracking, cancel retry controls |
| `FakeCacheService` | `Fakes/FakeCacheService.cs` | `ConcurrentDictionary`-based, no Redis dependency |
| `EventBuilder` | `Builders/EventBuilder.cs` | Fluent builder for Event aggregates |
| `BookingBuilder` | `Builders/BookingBuilder.cs` | Fluent builder for Booking aggregates |

### 8.4 Domain Unit Tests (~58+ tests)

| File | Tests | Coverage |
|------|-------|---------|
| `EventNameTests.cs` | 5 | EventName VO validation |
| `EmailTests.cs` | 8 | Email VO format + equality |
| `MoneyTests.cs` | 7 | Money VO arithmetic |
| `EventTests.cs` | 15 | Event aggregate lifecycle (create, publish, cancel, ticket types, seat ops) |
| `EventPhotoTests.cs` | 18 | EventPhoto entity validation, cover lifecycle |
| `EventPhotoManagementTests.cs` | 16 | Photo management through Event aggregate (add, remove, cover, caption, reorder, events) |
| `BookingTests.cs` | 14 | Booking aggregate state machine (pending -> confirmed -> cancelled -> refunded -> expired) |
| `BookingPendingPaymentTests.cs` | 16 | Pending-payment transition, confirmation, cancellation, expiration, active/modifiable checks |

### 8.5 Application Unit Tests (30+ tests)

| File | Tests | Coverage |
|------|-------|---------|
| `CreateBookingHandlerTests.cs` | 3 | CreateBooking happy path, auth failure, local-commit-before-payment ordering |
| `CreateBookingPendingFirstTests.cs` | 8 | Commit-before-payment invariant, idempotency key, deferred skip, durable compensation staging |
| `CompensationProcessorTests.cs` | 8 | Retry backoff, cancellation dispatch, unknown compensation type, max-retry boundary |
| `ConfirmDeferredPaymentHandlerTests.cs` | 5 | Deferred payment by reference code — all guard clauses, success + cache invalidation |
| `CancelBookingHandlerTests.cs` | 7 | Cancel authorization (attendee vs admin), state guards, seat release, commit failure |

### 8.6 Integration Tests (~30+ tests)

| File | Tests | Coverage |
|------|-------|---------|
| `OutboxTests.cs` | 5 | Transactional outbox atomic commitment, payload serialization, processing lifecycle, idempotency |
| `StripeWebhookTests.cs` | 4 | Webhook signature verification, event handling, payment status logic |
| `PaymentReconciliationTests.cs` | 1 | Orphaned booking detection, payment cancellation |
| `CreateBookingTests.cs` | 2 | HTTP booking creation happy path + 404 |
| `BookingFlowTests.cs` | 5 | Payment failure, deferred confirmation, capacity conflict, expiration + seat release, dead-letter requeue |
| `PendingFirstBookingTests.cs` | 6 | Local-commit-first payment path, durable compensation, retry/dead-letter flow, duplicate webhook idempotency |
| `GetEventTests.cs` | 3 | Event list + detail endpoints |
| `EventPhotoTests.cs` | 9 | Photo CRUD via HTTP (upload, cover, delete, auth) |

### 8.7 Concurrency Tests (~10 tests)

| File | Tests | Coverage |
|------|-------|---------|
| `LastTicketRaceTests.cs` | 2 | Multiple users competing for last ticket — optimistic concurrency prevents overbooking |
| `InventoryScaleTests.cs` | 3 | Scale invariants — up to 1000 concurrent users, capacity never exceeded |
| `EventIsolationTests.cs` | 2 | Events don't cross-contaminate inventory |
| `OutboxWebhookRaceTests.cs` | 1 | Race between outbox auto-confirm + webhook confirm — idempotency prevents double-confirm |
| `WebhookDoubleDeliveryRaceTests.cs` | 1 | 5 concurrent webhook deliveries for same booking — confirms exactly once |

**ConcurrentExecutor** — uses `TaskCompletionSource` as async barrier: all workers start simultaneously via `Task.Run`, maximizing race condition probability without blocking threads. 30-second timeout. Returns `ConcurrentResult` with success/failure counts.

### 8.8 Key Architectural Decisions in Testing
1. **Testcontainers over SQLite in-memory** — real SQL Server 2022 for maximum fidelity (catches SQL-specific locking bugs)
2. **Respawn over transaction rollback** — wipes data while keeping schema (migrations)
3. **TestAuthenticationHandler over JWT generation** — deterministic, no clock skew
4. **FakeCacheService over Redis container** — avoids another container dependency
5. **FakePaymentService with fail mode toggle** — allows testing payment failure paths without Stripe
6. **OutboxTestDriver** — deterministic, test-triggered outbox processing (replaces background workers)
7. **ConcurrentExecutor with async barrier** — all workers start simultaneously, maximizing race condition probability

---

## 9. Injected Enterprise Components (Forgotten Pieces)

These are critical architectural components that are typically omitted in standard projects but are mandatory for a production-grade, high-concurrency ticketing system like Eventy. Some are already implemented and documented below; others are recommended additions.

### 9.1 API Rate Limiting & Throttling Policies

**Purpose:** Prevent DDoS attacks and resource exhaustion during ticket rushes (flash sales) where thousands of users compete for limited inventory simultaneously.

**Current State:** Rate limiting is not yet implemented in the WebApi layer. The middleware pipeline (Program.cs) does not include `UseRateLimiter` or any rate limiting middleware.

**Recommended Implementation (ASP.NET Core 7+ Built-in Rate Limiting):**

- **Fixed Window Policy** for booking endpoint: `POST /api/booking` — limit to 5 requests per 30 seconds per authenticated user. This prevents automated bots from flooding the booking endpoint while allowing legitimate users to retry on concurrency conflicts.
  - Configuration key: `"RateLimiting:Booking:PermitLimit": 5`, `"RateLimiting:Booking:Window": "00:00:30"`
  - `AddRateLimiter(options => options.AddFixedWindowLimiter("booking", ...))`
  - Applied via `[EnableRateLimiting("booking")]` on `BookingController.CreateBooking`

- **Token Bucket Policy** for global API: 100 requests per 10 seconds per IP address. Allows burst traffic but smooths sustained load.
  - Configuration key: `"RateLimiting:Global:PermitLimit": 100`, replenished every 10 seconds
  - Applied via `app.UseRateLimiter()` in the middleware pipeline, after CORS, before authorization

- **Concurrency Limiter** for event creation: `POST /api/event` — limit to 1 concurrent request per organizer. Prevents race conditions in event creation where duplicate events could be created by double-submission.
  - `AddConcurrencyLimiter("event-create", permitLimit: 1, queueLimit: 10)`

- **Redis-backed distributed rate limiting:** For multi-instance deployments, use `System.Threading.RateLimiting` with a Redis-backed `PartitionedRateLimiter` to share rate limit state across API instances. The existing `ConnectionMultiplexer` singleton can be leveraged.

**Integration point:** Would be added to `Program.cs` between `UseCors` and `UseAuthorization` in the middleware pipeline, and to `Infrastructure/DependencyInjection.cs` for service registration.

### 9.2 Idempotency Keys & Consumers

**Purpose:** Prevent duplicate ticket purchases or double payments when the same request is delivered multiple times (network retries, webhook redelivery, user double-click).

**Current Implementation:** Eventy implements idempotency at three levels:

#### Level 1: ProcessedEvents Table (Infrastructure)
- **Entity:** `ProcessedEvent` (PK: `EventId` Guid, unique index on `IdempotencyKey`, `ProcessedAt`)
- **Store:** `IIdempotencyStore` with three modes:
  - `IsProcessedAsync(Guid eventId, ct)` — check if event/booking already processed
  - `MarkAsProcessedAsync(Guid, key, time, ct)` — synchronous save (immediate persist)
  - `MarkAsProcessed(Guid, key, time)` — track-only (no SaveChanges; caller commits atomically with its own changes via `CommitAsync`)

#### Level 2: Booking Event Handlers (Application)
- **BookingCreatedEventHandler:** Checks `IsProcessedAsync(bookingIdValue)` before processing. Uses idempotency key `"booking-created:{bookingIdValue}"`. Calls `MarkAsProcessed()` (track-only) THEN `CommitAsync()` — atomic: crash means neither booked-confirmation nor idempotency-mark persist.
- **BookingCancelledEventHandler:** Checks `IsProcessedAsync(bookingIdValue)`. Uses key `"booking-cancelled:{bookingIdValue}"`. Calls `MarkAsProcessed()` + `CommitWithoutEventsAsync()`.

#### Level 3: Webhook Confirmation (Application)
- **ConfirmBookingFromWebhookCommandHandler:** Uses deterministic GUID from `StripeEventId` (MD5 hash) as the idempotency key. If already processed, returns success. If booking already Confirmed, returns success (idempotent).
- This handles Stripe's at-least-once webhook delivery guarantee — the same `checkout.session.completed` event may be delivered multiple times.

#### Level 4: Outbox Message Idempotency (Infrastructure)
- **OutboxMessageService.ComputeIdempotencyKey:** If correlation ID exists: `"{EventName}_{CorrelationId}_{MessageId:N}"` (truncated to 100 chars). Otherwise: SHA-256 hash. Unique index on `IdempotencyKey` in `OutboxMessages` table prevents duplicate processing.

**Idempotency Key Semantics:** Uses the booking's `BookingId.Value` (deterministic per outbox message payload) rather than `DomainEvent.Id` (randomly regenerated on each deserialization via `Guid.NewGuid()`, useless for idempotency).

**Testing:** Concurrency tests verify idempotency under concurrent delivery:
- `WebhookDoubleDeliveryRaceTests` — 5 concurrent webhook deliveries confirm exactly once
- `OutboxWebhookRaceTests` — outbox auto-confirm + webhook confirm race prevents double-confirm

### 9.3 Resilience & Retry Policies (Polly)

**Purpose:** Handle transient faults (database timeouts, network blips, Redis connection drops) gracefully without crashing the application or losing data.

**Current Implementation:**

#### EF Core Built-in Retries
- `ApplicationDbContext` configured with `MaxBatchSize` and `EnableRetryOnFailure(3)` in the SQL Server provider — this is EF Core's built-in Polly-like retry for transient SQL errors (connection drops, timeouts).
- 300-second command timeout for long-running queries.

#### Concurrency Retry (Application Layer)
- `CreateBookingCommandHandler` implements a **3-attempt retry loop** for `ConcurrencyException`. On each retry, it creates a fresh DI scope (via `IServiceScopeFactory`) to get new repository instances and re-read the aggregate. If all 3 attempts fail, the error propagates.
- Payment initiation uses a local-commit-first sequence: commit booking + reserved seats + outbox atomically, then call Stripe with `payment-initiate:{bookingId}`.
- If the external call fails or throws after the local commit, the handler stages a `CompensationLogDto` and commits it with `CommitWithoutEventsAsync`; `CompensationProcessor` performs the eventual `CancelPaymentAsync` retry path.

#### Outbox Retry (Infrastructure)
- `OutboxDispatcher` implements a **3-attempt retry with exponential backoff**: Attempt 1 -> retry after 5s, Attempt 2 -> retry after 1min, Attempt 3 -> dead letter.
- Non-retryable errors (deserialization failures: `EventTypeNotFound`, `JsonDeserializationFailed`, `DeserializationReturnedNull`) are immediately dead-lettered.

#### Redis Graceful Degradation
- `RedisCacheService` wraps all operations in try/catch. On Redis failure, logs warning and returns null/skips — application continues without cache. This is a form of circuit breaker: if Redis is down, the system degrades to direct database access.

**Recommended Additional Polly Policies (not yet implemented):**

- **Circuit Breaker** for Stripe payment gateway: Break after 5 consecutive failures, open for 30 seconds. Prevents cascading failure when Stripe is down. Would wrap `IPaymentService.InitiatePaymentAsync` in `OutboxDispatcher` or `CreateBookingCommandHandler`.
  - Package: `Polly` (Microsoft.Extensions.Http.Resilience for HTTP clients)
  - Policy: `Policy.Handle<StripeException>().CircuitBreakerAsync(5, TimeSpan.FromSeconds(30))`

- **Timeout Policy** for database queries: 30-second timeout on all read queries via EF Core. Already partially implemented via `commandTimeout: 300` but could be more granular per query type.

- **Bulkhead Isolation** for outbox processing: Limit concurrent outbox dispatch to 10 parallel handlers to prevent thread pool exhaustion during event storms (e.g., after a flash sale where thousands of bookings are created).

### 9.4 Distributed Caching Topology (Redis Cache-Aside)

**Purpose:** Reduce database load for read-heavy queries (event listing, event details) during high-traffic periods like flash sales, while maintaining data consistency through post-commit invalidation.

**Current Implementation:** Redis Cache-Aside Pattern

#### Topology
- **Connection:** `ConnectionMultiplexer` registered as **Singleton** (designed to be shared across requests). Connection string from `ConnectionStrings:Redis` (default `localhost:6379`).
- **Cache Service:** `RedisCacheService` registered as **Singleton** (shares multiplexer).
- **Key Namespace:** All keys prefixed with `cache:` for isolation.
- **Serialization:** `System.Text.Json` with camelCase naming.

#### Cache-Aside Pattern (Read Path)
1. **Check cache:** Handler calls `ICacheService.GetAsync<T>(key, ct)` — if hit, returns cached data, no DB query
2. **Cache miss:** Handler queries database via `IApplicationReadDbContext`
3. **Populate cache:** Handler calls `ICacheService.SetAsync<T>(key, data, ttl, ct)`
4. **Return:** Data returned to caller

#### Cache Invalidation (Write Path — Post-Commit)
All invalidation runs **after** successful `UnitOfWork.CommitAsync()` — no eviction before transaction success. This guarantees that stale cache entries are only evicted after the new state is durable.

| Operation | Cache Eviction | Key Pattern | TTL |
|-----------|---------------|-------------|-----|
| CreateEvent | `RemoveByPatternAsync("events:*")` | `events:list:*` | 30s |
| UpdateEvent | Remove `event:details:{id}` + `RemoveByPatternAsync("events:*")` | targeted + pattern | 60s / 30s |
| PublishEvent | Remove `event:details:{id}` + `RemoveByPatternAsync("events:*")` | targeted + pattern | 60s / 30s |
| CreateBooking | Remove `event:details:{id}` | targeted | 60s |
| UploadEventPhotos | Remove `event:photos:{eventId}` | targeted | 120s |
| DeleteEventPhoto | Remove `event:photos:{eventId}` | targeted | 120s |
| ReorderEventPhotos | Remove `event:photos:{eventId}` | targeted | 120s |
| SetCoverPhoto | Remove BOTH `event:photos:{eventId}` + `event:details:{eventId}` | targeted | 120s / 60s |
| UpdatePhotoMetadata | Remove `event:photos:{eventId}` | targeted | 120s |

#### Cache Coverage Decisions
- **Cached (3/7 queries):** Events list (30s), Event details (60s), Event photos (120s) — nearly-static data, low mutation rate
- **Uncached (4/7 queries):** All booking queries (`GetBookingsByUser`, `GetBookingDetails`, `GetBookingByEvent`, `GetAllBookings`) — financial/personal data with high mutation rate; stale data risk outweighs cache benefit

#### Graceful Degradation
Redis failures never crash the application. All `RedisCacheService` methods wrap operations in try/catch:
- `GetAsync` -> returns null (cache miss, falls through to DB)
- `SetAsync` -> swallows error (no cache write, next read is a miss)
- `RemoveAsync` / `RemoveByPatternAsync` -> swallows error (stale entry expires via TTL)

**Recommended Enhancement: Write-Behind for Real-Time Ticket Availability**
For flash-sale scenarios where thousands of users query ticket availability simultaneously, a Write-Behind cache could be added:
- Redis maintains a real-time `available_count` per ticket type (key: `cache:ticket:avail:{ticketTypeId}`)
- On `ReserveSeats`, the WebSocket handler updates both the DB and Redis atomically
- Read path for availability checks reads from Redis (sub-millisecond) instead of DB
- A background job syncs Redis -> DB every 5 seconds for durability
- This would require a `IRealTimeInventoryService` abstraction and a `RedisInventoryService` implementation

### 9.5 Structured Diagnostics & Observability

**Purpose:** Enable distributed tracing across all layers (frontend -> API -> handler -> domain -> outbox -> background job) for debugging production issues, monitoring system health, and detecting anomalies.

**Current Implementation:**

#### Correlation IDs
- **CorrelationIdMiddleware** (WebApi) — assigns a `X-Correlation-Id` to every request. Reads existing header or generates new GUID. Stores in `HttpContext.Items["CorrelationId"]` and adds to response headers.
- **EventMetadataFactory** (Infrastructure) — creates `EventMetadata` with `CorrelationId` (from HttpContext items or new GUID), `CausationId` (new GUID), and `CreatedBy` (user ID). Used by `OutboxMessageService` for idempotency key computation.
- **Outbox Message Payload** — each outbox message carries the correlation ID, enabling end-to-end trace from HTTP request through outbox processing.

#### Structured Logging
- **LoggingPipelineBehavior** (Application) — logs `[START]` and `[END]` of every MediatR request with elapsed milliseconds
- **UnitOfWork** — logs number of changes and outbox events committed
- **OutboxProcessor** — logs polling cycles, processed/failed counts
- **OutboxDispatcher** — logs per-message dispatch, success/failure, retry scheduling
- **RedisCacheService** — logs cache hits/misses, invalidation counts, degradation warnings
- **WebSocketMiddleware** — logs connection lifecycle, seat lock attempts, collisions

**Recommended Additional Observability (OpenTelemetry + Serilog):**

- **OpenTelemetry Tracing:** Add `OpenTelemetry.Extensions.Hosting` and `OpenTelemetry.Instrumentation.AspNetCore` + `OpenTelemetry.Instrumentation.EntityFrameworkCore` packages to Infrastructure. Configure tracing with:
  - `AddSource("Eventy.Application")` — custom activity source for MediatR pipeline
  - `AddAspNetCoreInstrumentation()` — automatic HTTP request spans
  - `AddEntityFrameworkCoreInstrumentation()` — automatic DB query spans
  - Export to Jaeger/Zipkin/OTLP collector via `AddOtlpExporter()`

- **Serilog Structured Logging:** Replace the default `ILogger` with Serilog for enriched structured logs:
  - `Install-Package Serilog.AspNetCore` + `Serilog.Sinks.Console` + `Serilog.Enrichers.CorrelationId`
  - Enrichers: `WithCorrelationId()` (reads `X-Correlation-Id` header), `WithMachineName()`, `WithEnvironmentName()`
  - Output template: `{Timestamp:HH:mm:ss} [{Level:u3}] {CorrelationId} {SourceContext} {Message:lj}{NewLine}{Exception}`
  - Sinks: Console (dev), Seq (dev/staging), Application Insights (prod)

- **Health Checks:** Add `AspNetCore.HealthChecks.SqlServer` + `AspNetCore.HealthChecks.Redis`:
  - `AddHealthChecks().AddSqlServer(connectionString).AddRedis(redisConnectionString)`
  - `MapHealthChecks("/health")` in Program.cs
  - Kubernetes/Docker can use this for readiness/liveness probes

- **Metrics:** Add `System.Diagnostics.Metrics` counters:
  - `booking_created_total` (counter)
  - `booking_concurrent_in_flight` (gauge)
  - `outbox_processed_total` (counter)
  - `outbox_dead_lettered_total` (counter)
  - `cache_hit_rate` (gauge)
  - `seat_lock_collisions_total` (counter)

---

## 10. Exhaustive Cross-Layer Pattern Matrix

### Domain Layer

| Pattern | Implementation Location | Responsibility |
|---------|------------------------|----------------|
| **Aggregate Root** | `Domain/Aggregates/EventAggregate/Event.cs` | Consistency boundary for Event + TicketTypes + Photos. Enforces invariants: capacity limits, status transitions, duplicate ticket names |
| **Aggregate Root** | `Domain/Aggregates/BookingAggregate/Booking.cs` | Consistency boundary for Booking. Enforces state machine: Pending -> Confirmed/Cancelled/Expired -> Refunded |
| **Aggregate Root** | `Domain/Aggregates/UserAggregate/User.cs` | Consistency boundary for User + RefreshTokens. Manages token lifecycle |
| **Entity** (within aggregate) | `Domain/Aggregates/EventAggregate/Entities/TicketType.cs` | Seat accounting (Reserved/Sold/Available). Internal methods — only Event can call |
| **Entity** (within aggregate) | `Domain/Aggregates/EventAggregate/Entities/EventPhoto.cs` | Photo metadata, cover flag, display order |
| **Entity** (within aggregate) | `Domain/Aggregates/UserAggregate/RefreshToken.cs` | Refresh token lifecycle (active/revoked) |
| **Value Object** | `Domain/Primitives/Money.cs` | Monetary amount with currency. Immutable, structural equality. Arithmetic returns Result<T> |
| **Value Object** | `Domain/Primitives/Address.cs` | Physical address with geolocation. Immutable, validated |
| **Value Object** (ID types) | `Domain/Aggregates/*/ValueObject/*.cs` (7 files) | Strongly-typed GUID identifiers. FromDatabase() for EF Core, Create() for new |
| **Value Object** | `Domain/Aggregates/UserAggregate/ValueObject/Role.cs` | Smart Enum pattern (Attendee, Organizer, Admin) |
| **Domain Events** | `Domain/Aggregates/*/Events/*.cs` (20 events) | Record of something that happened in the domain. Carries aggregate ID + relevant data |
| **Domain Invariants** | All aggregate methods | Guard clauses returning `Result.Failure(...)` with specific error codes. E.g., cannot publish without ticket types, cannot reserve more than available |
| **Factory Method** | `Event.Create()`, `Booking.Create()`, `User.Create()`, `TicketType.Create()`, `EventPhoto.Create()` | Encapsulates construction with validation. Returns `Result<T>` on failure |
| **State Machine** | `Event` (Draft->Published->Cancelled/Completed), `Booking` (Pending->Confirmed->Cancelled->Expired->Refunded) | Explicit state transitions with pre/post conditions |
| **Result Pattern** | `Domain/Common/Result.cs`, `Result<TValue>` | Returns success/failure without exceptions. Carries `Error[]` on failure. Implicit conversion from TValue |
| **Error Factory** | `Domain/Errors/*.cs` (7 static classes) | Centralized error code definitions with typed `ErrorType` |
| **Specification** (implicit) | `TicketType.Remove()` (Sold==0 && Reserved==0), `Event.Publish()` (has ticket types, date in future) | Inline business rules that must be satisfied before state change |

### Application Layer

| Pattern | Implementation Location | Responsibility |
|---------|------------------------|----------------|
| **CQRS Command** | `Features/*/Commands/*/XxxCommand.cs` (19 commands) | Write-side request. Implements `ICommand` or `ICommand<TResponse>` |
| **CQRS Query** | `Features/*/Queries/*/XxxQuery.cs` (7 queries) | Read-side request. Implements `IQuery<TResponse>` |
| **Command Handler** | `Features/*/Commands/*/XxxCommandHandler.cs` (19 handlers) | Orchestrates domain operations, repository access, caching, UoW commit |
| **Query Handler** | `Features/*/Queries/*/XxxHandler.cs` (7 handlers) | Read-side projections via `IApplicationReadDbContext` (AsNoTracking) |
| **MediatR Pipeline Behavior** | `Abstractions/Behaviors/ValidationPipelineBehavior.cs` | Pre-handler validation. Runs all FluentValidation validators, short-circuits on failure |
| **MediatR Pipeline Behavior** | `Abstractions/Behaviors/LoggingPipelineBehavior.cs` | Pre/post-handler logging with elapsed time |
| **FluentValidation** | `Features/*/Commands/*/XxxValidator.cs` (7 validators) | Declarative validation rules (NotEmpty, MaxLength, EmailAddress, regex) |
| **Event Validator** | `Features/Bookings/Events/BookingCreated/BookingCreatedEventValidator.cs` | Pre-processing validation for domain events before handler runs |
| **Domain Event Handler** | `Features/Bookings/Events/BookingCreated/BookingCreatedEventHandler.cs` | Marks creation event as processed and waits for webhook/deferred confirmation. Implements `IDomainEventHandler<T>` |
| **Domain Event Handler** | `Features/Bookings/Events/BookingCancelled/BookingCancelledEventHandler.cs` | Log-only handler. Marks idempotency. Implements `IDomainEventHandler<T>` |
| **Result Pattern Wrapper** | All handlers return `Result<T>` or `Result` | Never throw exceptions for expected failures. Errors carry typed `ErrorType` |
| **Idempotency Guard** | `BookingCreatedEventHandler`, `BookingCancelledEventHandler`, `ConfirmBookingFromWebhookCommandHandler` | `IsProcessedAsync` check before processing. `MarkAsProcessed` (track-only) for atomic commit |
| **Retry Pattern** | `CreateBookingCommandHandler` (3-attempt loop) | Retries on `ConcurrencyException`. Creates fresh DI scope per attempt |
| **Durable Compensation Pattern** | `CreateBookingCommandHandler` + `CompensationProcessor` | Stages payment cleanup records after external failure and retries cancellation with backoff/dead-letter fallback |
| **Cache-Aside Pattern** | `GetEventsHandler`, `GetEventDetailsHandler`, `GetEventPhotosQueryHandler` | Check cache -> DB on miss -> populate cache -> return. Post-commit invalidation |
| **Dependency Injection** | `DependencyInjection.cs` | Registers validators, event handlers, pipeline behaviors, MediatR |
| **Dependency Inversion** | All abstractions in `Abstractions/` (15 interfaces) | Handlers depend on interfaces, not concrete implementations |

### Infrastructure Layer

| Pattern | Implementation Location | Responsibility |
|---------|------------------------|----------------|
| **Repository Pattern** | `Persistence/Repositories/*.cs` (6 repos) | Mediates between domain and data mapping. Encapsulates EF Core queries |
| **Unit of Work** | `Persistence/UnitOfWork.cs` | Atomic commit: extracts domain events, stages outbox, saves all in one transaction |
| **Transactional Outbox** | `Persistence/Outbox/OutboxMessageService.cs` + `BackgroundJobs/OutboxProcessor.cs` + `OutboxDispatcher.cs` | Guarantees event delivery even on crash. Events stored in same transaction as entity changes |
| **Outbox Locking** | `OutboxRepository.GetAndLockUnprocessedMessagesAsync` | Raw SQL `UPDATE TOP(n) WITH (UPDLOCK, READPAST, ROWLOCK) SET ProcessingLock=... OUTPUT INSERTED.*` — atomic lock-and-fetch with 5-min stale-lock reclaim |
| **Dead Letter Queue** | `OutboxRepository.MoveToDeadLetterAsync`, `CompensationLogRepository.MoveToDeadLetterAsync`, `OutboxDeadLetter.cs`, `AdminController` | After retry exhaustion or non-retryable error, moves records to `OutboxDeadLetters`. Admin can requeue |
| **Idempotency Store** | `Persistence/Repositories/IdempotencyStore.cs` | `ProcessedEvents` table with unique IdempotencyKey. Track-only mode for atomic commits |
| **Optimistic Concurrency** | EF Core `RowVersion` (rowversion) on Event, Booking, TicketType, User | `UnitOfWork` wraps `DbUpdateConcurrencyException` as `ConcurrencyException` |
| **Pessimistic Locking** | `BookingRepository.GetExpiredPendingBookingsAsync` | Raw SQL `UPDLOCK, READPAST, ROWLOCK` for safe concurrent Pending/PendingPayment expiry dequeue |
| **Fluent API Configuration** | `Persistence/Configuration/*.cs` (8 configs) | Entity-to-table mapping with value conversions, owned types, indexes, FKs |
| **Value Object Conversion** | `EventConfiguration`, `BookingConfiguration`, `UserConfiguration` | `HasConversion` for ID types (Guid <-> ValueObject), `OwnsOne` for Money/Address/Email |
| **Event Serializer** | `Persistence/Outbox/EventSerializer.cs` | Reflective type discovery + JSON serialization of domain events. Builds type dictionaries in static constructor |
| **JSON Converter Factory** | `Persistence/Outbox/Converters/ValueObjectJsonConverterFactory.cs` | Reflective converter for value objects with `Guid Value` + `FromDatabase(Guid)` |
| **Cache-Aside (Redis)** | `Caching/RedisCacheService.cs` | Get/Set/Remove/RemoveByPattern. Graceful degradation on Redis failure |
| **Background Service** | `OutboxProcessor` (5s), `CompensationProcessor` (10s), `BookingExpirationJob` (1min), `PaymentReconciliationJob` (2min) | `BackgroundService` base class. Independent DI scopes per iteration |
| **Adapter Pattern** | `ReadDbContextAdapter.cs` | Adapts `ApplicationDbContext` to `IApplicationReadDbContext` with AsNoTracking |
| **Strategy Pattern** | `StripePaymentGateway` vs `MockPaymentGateway` | `IPaymentService` with two implementations, selected via `Stripe:UseMock` config |
| **Factory Pattern** | `EventMetadataFactory.cs` | Creates correlation/causation metadata for outbox messages |
| **Connection Manager** | `RealTime/WebSocketConnectionManager.cs` | `ConcurrentDictionary`-based WebSocket registry per event |
| **Pub/Sub Broadcaster** | `RealTime/RedisPubSubBroadcaster.cs` | Redis pub/sub for cross-instance seat-state broadcasting |
| **Password Hashing** | `Authentication/PasswordHasher.cs` | BCrypt with work factor 12 |
| **JWT Generation** | `Authentication/JwtTokenGenerator.cs` | HmacSha256 signing, Jti claim, refresh token generation/hashing |
| **File Storage** | `Storage/LocalFileStorageService.cs` | Local file upload with validation (5MB, jpeg/png/webp), unique filenames |
| **Database Seeding** | `Persistence/SeedData/DatabaseSeeder.cs` | Seeds 12 users, 18 events, venue-mapped ticket types, ~75 bookings |
| **Design-time Factory** | `ApplicationDbContextFactory.cs` | `IDesignTimeDbContextFactory` for `dotnet ef` migrations |

### Presentation / Frontend Layer

| Pattern | Implementation Location | Responsibility |
|---------|------------------------|----------------|
| **Standalone Component** | All 23 components (no NgModules) | Self-contained, lazy-loadable. Angular 19 standalone-first |
| **Signal State Store** | `VenueViewStore`, `AuthApplicationService`, `EventApplicationService`, per-component signals | Reactive state management without NgRx. Computed signals for derived state |
| **HTTP Interceptor** (Functional) | `infrastructure/interceptors/auth.interceptor.ts` | Attaches JWT Bearer token to outgoing requests |
| **HTTP Interceptor** (Functional) | `infrastructure/interceptors/error.interceptor.ts` | Global error handling: 401->logout, 403->/unauthorized, 400->toast |
| **Route Guard** (Functional) | `core/guards/auth.guard.ts` | `CanActivateFn` — redirects unauthenticated users to /login |
| **Route Guard** (Factory) | `core/guards/role.guard.ts` | `roleGuard(roles[])` — role-based access control |
| **Directive** | `infrastructure/directives/img-fallback.directive.ts` | Hides broken images (host listener on error event) |
| **Result Pattern** (Frontend) | `core/models/result.model.ts` | `Result<T>` envelope matching backend CQRS Result pattern |
| **Mapper Pattern** | `core/mappers/event.mapper.ts`, `booking.mapper.ts` | Maps backend DTOs to frontend models |
| **Application Service** (Facade) | `application/services/*.service.ts` (4 services) | Orchestrates HTTP services, caching, mapping, signal state |
| **HTTP Service** (Base class) | `application/http/http-client-base.ts` | Abstract base with `toErrorResult()` for ProblemDetails parsing |
| **D3.js Rendering** | `venue-graph-renderer.service.ts` | Direct SVG manipulation bypassing Angular change detection for performance |
| **WebSocket Client** | `live-state-sync.service.ts` | Real-time seat sync with auto-reconnect, heartbeat, status signals |
| **Optimistic Locking** (Frontend) | `seat-lock-orchestrator.service.ts` | `acquire()` sends LOCK, optimistic UI update, collision rollback |
| **Cache-Aside** (Frontend) | `event-application.service.ts` | Map-based cache with 30s TTL + `shareReplay` for event list |
| **Observer Pattern** | RxJS Subjects in `LiveStateSyncService`, `SeatLockOrchestratorService` | `deltas$`, `collisions$`, `acks$` observables for event-driven updates |
| **Composite Pattern** | `SeatingChartComponent` wires store + renderer + sync + orchestrator | Composes multiple services into a single seating chart experience |
| **Template-Driven Filtering** | `EventListComponent` sidebar filters | Category, date, price range, sort, nearby toggle bound to signals |
| **Multi-Step Wizard** | `EventCreateComponent` | Progress indicator, step navigation (Details -> Date&Location -> Ticket Types) |
| **Lazy Loading** | `app.routes.ts` | `loadComponent` for all feature routes — reduced initial bundle |

---

## 11. End-to-End Workflow Trace: Ticket Booking / حجز التذاكر

This section traces the complete ticket booking workflow chronologically from the user's click on the frontend to the final asynchronous confirmation via the outbox pattern. Each stage names the exact folder, class, method, and runtime boundary crossed.

### Stage 1: Frontend — User Interaction & HTTP Request Initiation

**1.1 User Click on "Book Ticket"**
- **Component:** `EventiyApp/src/app/features/events/event-detail/event-detail.component.ts`
- The user has navigated to the event detail page (`/events/{id}`). The component has loaded the event data via `EventApplicationService.getEvent(id)`. The user selects a ticket type from the dropdown, sets quantity via the stepper, selects payment method (Instant/Deferred), and clicks "Book Ticket".
- The click handler calls `bookingApplicationService.createBooking(...)`.

**1.2 Application Service Orchestration**
- **Service:** `EventiyApp/src/app/application/services/booking-application.service.ts`
- The `BookingApplicationService.createBooking()` method constructs a `CreateBookingRequest` object with `{ eventId, ticketTypeId, quantity, paymentMethod }` (matching `core/models/booking.model.ts`).
- Delegates to `BookingHttpService.createBooking(data)`.

**1.3 HTTP Service**
- **Service:** `EventiyApp/src/app/application/http/booking.http-service.ts`
- Extends `HttpClientBase` with base URL `/api/booking`.
- Calls `this.http.post<Result<CreateBookingResponse>>()` to `${this.apiUrl}`.
- The `Result<T>` envelope (from `core/models/result.model.ts`) matches the backend CQRS Result pattern — `{ isSuccess, isFailure, value?, errors? }`.

**1.4 HTTP Interceptor — JWT Injection**
- **Interceptor:** `EventiyApp/src/app/infrastructure/interceptors/auth.interceptor.ts`
- Functional interceptor (`HttpInterceptorFn`) registered in `app.config.ts` via `provideHttpClient(withInterceptors([authInterceptor, errorInterceptor]))`.
- Checks if the request URL is NOT `/api/auth/login` or `/api/auth/register`.
- Retrieves JWT token from `AuthApplicationService.getToken()` (stored in `sessionStorage` — BUG #19 fix).
- Clones the request with `Authorization: Bearer {token}` header.
- Request goes over the network to `https://localhost:7001/api/booking`.

**1.5 Error Interceptor (Post-Response)**
- **Interceptor:** `EventiyApp/src/app/infrastructure/interceptors/error.interceptor.ts`
- If the response is an error:
  - 401 -> `AuthApplicationService.logout()` (session expired)
  - 403 -> `router.navigate(['/unauthorized'])`
  - 400 -> `ToastService.showError()` with validation error messages from ProblemDetails
  - 409 -> `ToastService.showError()` ("Conflict — another user may have booked the same seats")
  - 500+ -> `ToastService.showError()` ("Server error")
- Re-throws the error after toasting.

### Stage 2: Presentation & Middleware — Request Interception

**2.1 CorrelationIdMiddleware**
- **File:** `Eventy.WebApi/Middlewares/CorrelationIdMiddleware.cs`
- **First middleware in the pipeline.** Reads `X-Correlation-Id` header from the HTTP request. If absent, generates a new `Guid.NewGuid()`.
- Stores it in `HttpContext.Items["CorrelationId"]` for downstream access.
- Adds it to the response headers as `X-Correlation-Id`.
- Logs at Debug: `"Request {Path} assigned correlation {CorrelationId}"`.

**2.2 WebSocket Middleware (Skipped for REST)**
- **File:** `Eventy.WebApi/Middlewares/WebSocketMiddleware.cs`
- This request is `POST /api/booking`, not a WebSocket upgrade to `/ws/venues/...`. The middleware checks `context.Request.Path.StartsWithSegments("/ws/venues")` and skips.

**2.3 GlobalExceptionHandlingMiddleware**
- **File:** `Eventy.WebApi/Middlewares/GlobalExceptionHandlingMiddleware.cs`
- Wraps the entire downstream pipeline in a try/catch. If any exception propagates, it converts to `ProblemDetails`:
  - `ConcurrencyException` -> 409 Conflict, `concurrency.conflict`
  - `DbUpdateException` -> 500, `database.error`
  - `UnauthorizedAccessException` -> 403, `unauthorized`
  - Catch-all -> 500, `server.error`
- For the booking flow, the most relevant is `ConcurrencyException` (409) — returned when two users race for the same seat.

**2.4 CORS & Authorization**
- `UseCors("AllowAngular")` — verifies the `Origin` header matches allowed origins (`localhost:57354` in dev, configured list in prod).
- `UseAuthorization()` — evaluates the `[Authorize]` attribute on `BookingController`.
- JWT Bearer authentication handler (configured in `Infrastructure/DependencyInjection.cs`) validates:
  - `ValidateIssuer = true` (must be `Eventiy.Api`)
  - `ValidateAudience = true` (must be `Eventiy.Client`)
  - `ValidateLifetime = true` (token must not be expired — 1440 min / 24h)
  - `ValidateIssuerSigningKey = true` (HmacSha256 signature with secret from User Secrets/env vars)
- On success, sets `HttpContext.User` with claims: `NameIdentifier` (user GUID), `Email`, `Role`.
- On failure, returns 401 Unauthorized.

**2.5 Model Binding**
- `BookingController` has class-level `[Authorize]` and route `api/[controller]`.
- The `CreateBooking` action: `[HttpPost] public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)`.
- ASP.NET Core's `[ApiController]` behavior automatically:
  - Deserializes JSON body to `CreateBookingRequest` (a record in `RequestsDesign/`).
  - Validates required fields, returns 400 if model state is invalid.

### Stage 3: Application Pipeline — MediatR Dispatch & Validation

**3.1 Controller Dispatches Command**
- **File:** `Eventy.WebApi/Controllers/BookingController.cs`
- `CreateBooking` constructs `CreateBookingCommand` from the request body: `new CreateBookingCommand(request.EventId, request.TicketTypeId, request.Quantity, request.PaymentMethod)`.
- Calls `_sender.Send(command, ct)` where `_sender` is MediatR's `ISender`.

**3.2 LoggingPipelineBehavior**
- **File:** `Application/Abstractions/Behaviors/LoggingPipelineBehavior.cs`
- First pipeline behavior to execute (registered as transient).
- Logs `[START] Handling CreateBookingCommand`.
- Starts `Stopwatch`.

**3.3 ValidationPipelineBehavior**
- **File:** `Application/Abstractions/Behaviors/ValidationPipelineBehavior.cs`
- **Constraint:** `where TResponse : IValidationResult<TResponse>` — `Result<CreateBookingResponse>` implements `IValidationResult<TCreateBookingResponse>` via the `Result<TValue>` type.
- Injects `IEnumerable<IValidator<CreateBookingCommand>>` — resolves `CreateBookingCommandValidator` (registered via `AddValidatorsFromAssembly`).
- Runs `CreateBookingCommandValidator`:
  - `EventId`: `NotEmpty()`
  - `TicketTypeId`: `NotEmpty()`
  - `Quantity`: `GreaterThan(0)`
- If validation fails: collects all errors, converts to `Error.Validation(...)`, returns `TResponse.CreateFailure(errors)` — short-circuits, handler NOT called.
- If validation passes: calls `next()` to proceed to handler.

**3.4 Command Handler Resolution**
- MediatR resolves `CreateBookingCommandHandler` from the DI container (registered via `RegisterServicesFromAssembly` in `Application/DependencyInjection.cs`).
- Handler constructor injects: `IServiceScopeFactory` (for scoped retry), `ICurrentUserService`, `TimeProvider`, `IPaymentService`, `ICacheService`.

### Stage 4: Domain Execution — Aggregate Loading & Invariant Enforcement

**4.1 Get Current User**
- `CreateBookingCommandHandler` calls `ICurrentUserService.GetCurrentUserId()`.
- `CurrentUserService` (Infrastructure) reads `ClaimTypes.NameIdentifier` from `HttpContext.User`, parses to `Guid`, returns `Result<UserId>`.
- If not authenticated, returns `Failure(UserErrors.UserNotAuthenticated)`.

**4.2 Validate IDs**
- `TicketTypeId.Create(command.TicketTypeId)` — rejects `Guid.Empty`, returns `Result<TicketTypeId>`.
- `EventId.Create(command.EventId)` — same.

**4.3 Retry Loop (3 attempts for ConcurrencyException)**
- The handler enters a retry loop: `for (int attempt = 0; attempt < 3; attempt++)`.
- Each iteration creates a **fresh DI scope** via `_serviceScopeFactory.CreateScope()` — this gives new repository instances and a new `UnitOfWork` with fresh `ApplicationDbContext` state. This is critical: a stale DbContext from a failed attempt could have partially tracked entities.

**4.4 Load Event Aggregate**
- Calls `_eventRepository.GetByIdAsync(eventId, ct)` — `EventRepository.GetByIdAsync` executes:
  - `_context.Events.Include(e => e.TicketTypes).Include(e => e.Photos).FirstOrDefaultAsync(e => e.Id == eventId)`
- This loads the full aggregate root with all its child entities (TicketTypes and Photos) in a single query with JOINs.
- If not found, returns `Failure(EventErrors.EventNotFound)`.

**4.5 Find Ticket Type**
- Searches `@event.TicketTypes` for the matching `TicketTypeId`.
- If not found, returns `Failure(TicketTypeErrors.TicketTypeNotFoundInEvent)`.

**4.6 Domain Operation — Reserve Seats**
- Calls `@event.ReserveSeats(ticketTypeId, command.Quantity, utcNow)` on the **Event aggregate root**.
- `Event.ReserveSeats` delegates to `TicketType.ReserveSeats(quantity, utcNow)` (internal method):
  - **Invariant check 1:** `quantity > 0` — else `Failure(TicketTypeErrors.QuantityMustBeGreaterThanZero)`
  - **Invariant check 2:** `quantity <= AvailableCount` (where `AvailableCount = Capacity - SoldCount - ReservedCount`) — else `Failure(TicketTypeErrors.InsufficientAvailableSeats)`
  - On success: increments `ReservedCount += quantity`
- `Event.ReserveSeats` raises `TicketTypeSeatsReservedEvent` (carries `TicketTypeId, EventId, QuantityReserved, TotalSold, AvailableRemaining`).
- The domain event is added to the aggregate's `_domainEvents` collection.

**4.7 Create Booking Aggregate**
- `Booking.Create(userId, eventId, ticketTypeId, eventTitle, quantity, money, command.PaymentMethod, utcNow)`:
  - **Invariant check:** quantity 1-10, event title non-empty, money non-null and > 0
  - Sets `Status = Pending`
  - Calculates `TotalAmount = money.Amount * quantity`
  - **Payment method behavior:**
    - `Instant` -> `HoldExpiresAt = utcNow + 2 min`
    - `Deferred` -> generates `ReferenceCode` = `FAW-{cryptographic hex}`, `HoldExpiresAt = utcNow + 30 min`
  - Raises `BookingCreatedEvent`

**4.8 Prepare Local Commit**
- `Instant` bookings keep their short hold window and remain locally pending until Stripe confirms payment.
- `Deferred` bookings generate the Fawry-style reference code and skip Stripe checkout creation.
- The handler does not call an external payment provider before the local database transaction commits.

### Stage 5: Infrastructure & Persistence — Atomic Commit with Outbox

**5.1 Add Booking to Repository**
- `_bookingRepository.AddBookingAsync(booking, ct)` — adds to `Bookings` DbSet. Entity is tracked by EF Core ChangeTracker but NOT yet saved.

**5.2 UnitOfWork.CommitAsync — Domain Event Extraction**
- **File:** `Infrastructure/Persistence/UnitOfWork.cs`
- `CommitAsync(ct)` calls `ExtractDomainEvents()`:
  - Iterates `ChangeTracker.Entries<IAggregateRoot>()` — finds the `Event` and `Booking` aggregates.
  - Collects their `DomainEvents` (e.g., `TicketTypeSeatsReservedEvent`, `BookingCreatedEvent`).
  - Calls `ClearDomainEvents()` on each aggregate — events are removed from the aggregate but staged in the outbox.

**5.3 Outbox Staging (Same Transaction)**
- `_outboxService.AddFromDomainEvents(domainEvents)`:
  - For each domain event: generates a `Guid`, serializes via `IEventSerializer.Serialize<T>(@event)` (System.Text.Json with camelCase).
  - Computes idempotency key via `EventMetadataFactory.Create()` (correlation ID from HttpContext, causation ID, user ID).
  - Creates `OutboxMessageDto` with `EventName`, `Domain`, `Payload`, `OccurredOnUtc`, `IdempotencyKey`.
  - Calls `_outboxRepository.AddRange(messages)` — adds to `OutboxMessages` DbSet.
- **All of this is in the SAME DbContext transaction** — entities, outbox messages, and idempotency markers are staged together.

**5.4 SaveChangesAsync (Atomic Commit)**
- `_context.SaveChangesAsync()` — EF Core executes a single database transaction:
  - INSERT into `Bookings` table (new booking row with `RowVersion` generated by SQL Server)
  - UPDATE `TicketTypes` table (incremented `ReservedCount`, updated `RowVersion` via rowversion)
  - UPDATE `Events` table (if any event-level fields changed, `RowVersion` updated)
  - INSERT into `OutboxMessages` table (one row per domain event, `ProcessedOnUtc = NULL`)
- If any of these fail (e.g., `RowVersion` mismatch = another user modified the same row):
  - `DbUpdateConcurrencyException` is thrown.
  - `UnitOfWork` catches it and re-throws as `ConcurrencyException`.
  - The handler's retry loop catches `ConcurrencyException` and retries (up to 3 attempts).
  - On retry: re-reads fresh aggregate state and attempts the local commit again; no external payment session exists yet.
  - If all 3 attempts fail: `ConcurrencyException` propagates to `GlobalExceptionHandlingMiddleware` -> 409 Conflict response.
- On success: `rowsAffected > 0`. Database locks released. Transaction committed.

**5.5 Cache Invalidation (Post-Commit)**
- After successful commit, handler calls `_cacheService.RemoveAsync($"cache:event:details:{eventId}")`.
- This evicts the cached event details (which contain the old `ReservedCount`/`AvailableCount` values).
- Next request for event details will get a cache miss and read fresh data from DB.
- Runs AFTER `CommitAsync` returns — guarantees no cache eviction before transaction success.

**5.6 External Payment Initiation After Commit**
- For `Instant` payment: calls `IPaymentService.InitiatePaymentAsync(bookingId, referenceCode, amount, currency, idempotencyKey, ct)` with `idempotencyKey = "payment-initiate:{bookingId}"`.
  - If `StripePaymentGateway` (production): creates Stripe Checkout Session and passes the idempotency key in `RequestOptions.IdempotencyKey`.
  - If `MockPaymentGateway` (dev): returns fake `mock://payment/{bookingId}` URL.
- On success, the handler returns the payment URL/client secret to the caller.
- On failure or exception, the handler stages a `CompensationLogDto` and calls `CommitWithoutEventsAsync()` so cancellation work is durable and retried asynchronously.

**5.7 Return Result**
- Handler returns `Result<CreateBookingResponse>.Success(new CreateBookingResponse(bookingId, paymentUrl, clientSecret))`.
- `BookingController.CreateBooking` calls `result.ToCreatedResult("GetBookingDetails", new { id = result.Value.BookingId })`.
- `ResultExtensions.ToCreatedResult` returns `CreatedAtRouteResult` with 201 status and `Location: /api/booking/{bookingId}` header.

**5.8 LoggingPipelineBehavior (Post-Handler)**
- Logs `[END] CreateBookingCommand handled in {ElapsedMs}ms`.
- Stops `Stopwatch`.

**5.9 Response**
- HTTP 201 Created with JSON body containing `isSuccess: true`, `value: { bookingId, paymentUrl, clientSecret }`.
- `CorrelationIdMiddleware` adds `X-Correlation-Id` to response headers.
- `errorInterceptor` on the Angular side sees 201, passes through.
- `BookingApplicationService` returns the `CreateBookingResponse` to the component.
- The component navigates to `/bookings/{bookingId}` (or redirects to Stripe Checkout URL for Instant payment).

### Stage 6: Asynchronous Reliability — Outbox, Compensation, and Event Handling

**6.1 OutboxProcessor Polls**
- **File:** `Infrastructure/BackgroundJobs/OutboxProcessor.cs`
- Every 5 seconds, the `OutboxProcessor` (a `BackgroundService`) creates a fresh DI scope and calls:
  - `_outboxRepository.GetAndLockUnprocessedMessagesAsync(_lockId, _timeProvider, batchSize: 50, ct)`
  - This executes raw SQL: `UPDATE TOP(50) OutboxMessages WITH (UPDLOCK, READPAST, ROWLOCK) SET ProcessingLock=@LockId, ProcessingLockedAt=@Now OUTPUT INSERTED.* WHERE ProcessedOnUtc IS NULL AND (NextRetryOnUtc IS NULL OR NextRetryOnUtc <= @Now) AND (ProcessingLock IS NULL OR ProcessingLockedAt <= DATEADD(MINUTE,-5,@Now))`
  - Returns up to 50 unprocessed outbox messages as `AsNoTracking` DTOs, atomically locked by the processor's unique `_lockId`.

**6.2 OutboxDispatcher Deserializes**
- **File:** `Infrastructure/BackgroundJobs/OutboxDispatcher.cs`
- `DispatchBatchAsync` iterates each message:
  - Calls `IEventSerializer.Deserialize(message.EventName, message.Payload)` — looks up the type by name (e.g., "BookingCreatedEvent") in the static dictionary, deserializes the JSON payload to an `IDomainEvent` instance.
  - If deserialization fails with `EventTypeNotFound`, `JsonDeserializationFailed`, or `DeserializationReturnedNull` -> **non-retryable**, moves to dead letter immediately.

**6.3 Handler Resolution (Reflection-based)**
- Resolves `IDomainEventHandler<T>` via `serviceProvider.GetService(typeof(IDomainEventHandler<>).MakeGenericType(eventType))`.
- For `BookingCreatedEvent`, this resolves `BookingCreatedEventHandler` (registered in `Application/DependencyInjection.cs` as `IDomainEventHandler<BookingCreatedEvent>` -> `BookingCreatedEventHandler`, scoped).
- If no handler is registered, logs a warning and returns success (no-op).

**6.4 BookingCreatedEventHandler Executes**
- **File:** `Application/Features/Bookings/Events/BookingCreated/BookingCreatedEventHandler.cs`
- **Idempotency check:** Calls `IIdempotencyStore.IsProcessedAsync(bookingIdValue, ct)`. If already processed (e.g., from a previous redelivery), returns success without re-processing.
- **Business logic:** Logs that the booking is awaiting external confirmation and does not confirm instant bookings.
- **Idempotency mark:** Calls `MarkAsProcessed(bookingIdValue, "booking-created:{bookingIdValue}", utcNow)` and commits with `CommitWithoutEventsAsync()`.
- **Reason:** Stripe webhook confirmation and deferred reference confirmation are the only confirmation paths. This avoids a race between outbox delivery and webhook delivery.

**6.5 OutboxDispatcher Marks Processed**
- After all messages in the batch are dispatched:
  - `MarkRangeAsProcessedAsync(processedIds, now, ct)` — sets `ProcessedOnUtc` on successfully processed messages, clears lock fields.
  - `MarkRangeAsFailedAsync(failedMessages, ct)` — for failed messages: increments `RetryCount`, sets `Error`, computes `NextRetryOnUtc` (5s for attempt 1, 1min for attempt 2), clears lock.
  - `context.SaveChangesAsync()` — persists all changes.
- If a message has failed 3 times or has a non-retryable error: `MoveToDeadLetterAsync(messageId, error, movedAt, ct)` — copies the message to `OutboxDeadLetters` table and deletes from `OutboxMessages`.

**6.6 OutboxProcessor Releases Locks**
- After dispatch completes, `ReleaseLockAsync(_lockId, ct)` releases any remaining locks held by this processor instance.
- The processor waits 5 seconds, then polls again.

**6.7 Downstream Event Propagation**
- Confirmation events are produced by the webhook or deferred-payment command, not by `BookingCreatedEventHandler`.
- When confirmation succeeds, any resulting domain events are picked up by the outbox polling cycle and dispatched to registered handlers.
- Currently, unhandled events are logged as "no handler registered" and marked as processed (no-op).
- In a production system, additional handlers could update read models, send attendee notifications, trigger analytics, or notify organizers.

### Stage 7: Compensation, Expiration & Cleanup (Background Jobs)

**7.1 CompensationProcessor (every 10 seconds)**
- **File:** `Infrastructure/BackgroundJobs/CompensationProcessor.cs`
- Locks up to 50 ready compensation records with `UPDLOCK, READPAST, ROWLOCK`.
- Executes `CancelPaymentAsync` for payment-cancellation records.
- On success, marks the record processed.
- On failure, stores the error and schedules the next retry with backoff: 5s, 30s, 1m, 5m, 15m.
- After 5 failed attempts, moves the record to `OutboxDeadLetters`.

**7.2 BookingExpirationJob (every 1 minute)**
- **File:** `Infrastructure/BackgroundJobs/BookingExpirationJob.cs`
- Calls `GetExpiredPendingBookingsAsync(now, batchSize: 100, ct)` — raw SQL with `UPDLOCK, READPAST, ROWLOCK` to safely dequeue expired Pending/PendingPayment bookings without race conditions.
- For each expired booking:
  - Calls `booking.Expire(now)` — transitions to Expired, raises `BookingExpiredEvent`.
  - Loads the event and calls `evt.ReleaseSeats(booking.TicketTypeId, booking.Quantity, now)` — decrements ReservedCount on the TicketType.
- Commits via `UnitOfWork.CommitAsync` — stages `BookingExpiredEvent` and `TicketTypeSeatsReleasedEvent` to the outbox.
- The outbox processor will pick these up in the next cycle.

**7.3 PaymentReconciliationJob (every 2 minutes)**
- **File:** `Infrastructure/PaymentReconciliationJob.cs`
- Calls `GetPendingInstantBookingsPastHoldAsync(now, batchSize: 50, ct)` — finds Instant bookings whose 2-minute hold has expired (AsNoTracking, read-only).
- For each orphaned booking: calls `paymentService.CancelPaymentAsync(booking.Id.Value, ct)` to cancel the Stripe checkout session.
- Log-only on failure — does not modify booking state (the BookingExpirationJob handles the booking state transition).

---

## 12. Payment Reliability Posture

### Pending-First Flow

The booking flow persists local state before any external payment API call:

```text
ReserveSeats -> Booking.Create(Pending) -> AddBookingAsync -> CommitAsync
              -> InitiatePaymentAsync(..., idempotencyKey: payment-initiate:{bookingId})
              -> Stripe webhook confirms paid checkout sessions
```

This ordering removes the external-before-local dual-write failure mode. If the process crashes after the local commit but before payment initiation completes, the booking still exists with a hold window and can be expired by background jobs.

### Durable Compensation

Payment initiation failures after local commit are written as compensation records instead of being cleaned up only in an exception handler. The compensation table stores the booking ID, operation type, payload, idempotency key, retry count, next retry time, and processing lock. `CompensationProcessor` retries cleanup with backoff and moves exhausted records to dead letters.

### Confirmation Source of Truth

Instant bookings are confirmed by the signed Stripe webhook. `BookingCreatedEventHandler` marks its event as processed and logs the awaiting-confirmation state; it does not move instant bookings to `Confirmed`. Deferred bookings continue to use reference-code confirmation.

### Race Handling

| Race | Handling |
| --- | --- |
| Payment initiation retry | Deterministic provider idempotency key `payment-initiate:{bookingId}` |
| Duplicate webhook delivery | `ProcessedEvents` idempotency entry based on Stripe event ID |
| Compensation processor restart | Processing locks expire after the stale-lock window |
| User pays while cancellation is attempted | `CancelPaymentAsync` skips completed sessions |
| Expiration job vs compensation cleanup | Expiration releases local seats; compensation only cleans up the external session |

## 13. Security Hardening Posture

### JWT Security
- **Secret removed from source control:** `Jwt:Secret` removed from `appsettings.json`. Loaded via .NET configuration hierarchy: User Secrets (dev) -> Environment Variables/Azure Key Vault (prod).
- **Startup validation:** `Infrastructure/DependencyInjection.cs` throws if `Jwt:Secret` is null/empty or < 32 chars.
- **Git history rewrite:** `git filter-branch` replaced old hardcoded secret with `REVOKED_JWT_SECRET` across all 62 commits. Backup refs deleted, `git gc --aggressive --prune=now` run.
- **Token validation:** `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` all set to true.
- **Signing algorithm:** HmacSha256 with 64-char cryptographically-random secret.
- **Token expiry:** 1440 minutes (24 hours) for access token, 7 days for refresh token.
- **Refresh token storage:** Hashed (SHA-256) in database — raw token never stored. Set as HttpOnly, Secure, SameSite=Strict cookie.
- **Refresh token rotation:** On each refresh, old token is revoked and new one issued. If a revoked/expired token is presented, ALL refresh tokens for that user are revoked (reuse detection).
- **Frontend token storage:** `sessionStorage` (not `localStorage`) — BUG #19 fix. Session-scoped, cleared on tab close.

### CORS
- `.AllowCredentials()` enabled for cookie-based refresh token flow.
- Dev/prod origins split from config — no wildcard origins.
- `AllowAnyHeader()`, `AllowAnyMethod()` — but credentials restricted to specific origins.

### Authorization
- `[Authorize]` on `BookingController` (all endpoints require authentication).
- `[Authorize(Roles="Organizer,Admin")]` on event mutation endpoints.
- `[Authorize(Roles="Admin")]` on all admin controllers and organizer approval.
- Domain has zero authorization knowledge — role checks are in controllers and handlers via `ICurrentUserService`.

### File Upload Security
- Max file size: 5MB (`LocalFileStorageService`).
- Allowed content types: `image/jpeg`, `image/png`, `image/webp` only.
- Filenames sanitized: `{Guid:N}_{safeFileName}` — no path traversal possible.
- `MultipartBodyLengthLimit = 60MB` in `Program.cs` for form uploads.

### Stripe Webhook Security
- Raw request body read before any model binding (required for signature verification).
- `EventUtility.ConstructEvent` verifies HMAC-SHA256 signature against `Stripe:WebhookSecret`.
- Only `checkout.session.completed` events processed; others return 202 Accepted.
- `PaymentStatus == "paid"` checked before confirmation.

---

## 14. Key Decisions & Architectural Rationale

| Decision | Rationale |
|----------|-----------|
| `RedisCacheService` is Singleton | `ConnectionMultiplexer` designed to be shared; graceful degradation (log+skip) on Redis failures |
| Cache keys prefixed with `cache:` | Redis namespace isolation; prevents collision with other Redis users |
| Cache invalidation post-commit only | `RemoveByPatternAsync("events:*")` on create, targeted removal on update/booking — all AFTER `CommitAsync` returns |
| `MoveToDeadLetterAsync` copies then deletes | Clean separation, no soft-delete flags. Dead-letter table is a separate table, not a status flag. |
| `AdminController` injects `IOutboxRepository` directly | Infrastructure management tool, not business logic — bypasses MediatR pipeline |
| Bounding box (±0.18 deg ~ 20km) | Coarse SQL-level filter — sufficient for initial nearby query accuracy. Haversine post-filter removes corner false positives. |
| `EventType` enum values align with frontend `EVENT_CATEGORIES` | Type-safe category filtering across full stack without string mapping |
| `Jwt:Secret` loaded from User Secrets/env vars | No secret in source control. .NET config hierarchy handles dev/prod resolution. |
| Domain has zero framework dependencies | No MediatR, no EF, no ASP.NET, no `TimeProvider`. Pure DDD. Time passed as `DateTime utcNow` parameter. |
| Seat lifecycle: Reserve -> Confirm -> Release/Refund | `ReserveSeats` (Reserved++) -> `ConfirmReservation` (Reserved--, Sold++) -> `ReleaseSeats` (Reserved--) / `RefundSeats` (Sold--) |
| `executeFinalBooking()` bypasses `acquire()` short-circuit | Sends LOCK directly via `liveSync.send()`. `acquire()` kept for per-click optimistic flow. |
| Floating pill uses flex-flow natural positioning | Avoids `overflow: hidden` clipping. Shows CSS-only spinner during submission. |
| Payment-before-commit ordering | If Stripe is down, nothing is persisted. No orphaned booking with reserved seats. On commit failure, compensates by calling `CancelPaymentAsync`. |
| Idempotency key = `BookingId.Value` (not `DomainEvent.Id`) | `DomainEvent.Id` is `Guid.NewGuid()` — regenerated on each deserialization, useless for idempotency. `BookingId.Value` is deterministic per outbox message payload. |
| `MarkAsProcessed()` track-only before `CommitAsync()` | Crash between mark and commit means neither persists, so redelivery sees Pending booking and retries. Crash after commit means marker + confirmation both persisted, so redelivery short-circuits. |
| Booking queries uncached | Financial/personal data with high mutation rate. Stale data risk outweighs cache benefit. |
| No EF Core interceptors | Domain event extraction is explicit in `UnitOfWork.CommitAsync()`, not hidden in a `SaveChangesInterceptor`. Deliberate clarity choice. |
| Testcontainers over SQLite in-memory | Real SQL Server 2022 catches SQL-specific concurrency bugs (row-level locking, UPDLOCK/READPAST). |
| ConcurrentExecutor with async barrier | All workers start simultaneously via `TaskCompletionSource`, maximizing race condition probability without blocking threads. |

---

*End of Document*
