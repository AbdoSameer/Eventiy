# Eventiy — Full Architecture Reference

> Use this document to teach an LLM about the Eventiy project structure, patterns, conventions, and implementation phases.

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Solution Structure](#2-solution-structure)
3. [Backend Architecture (Clean Architecture + DDD + CQRS)](#3-backend-architecture)
4. [Frontend Architecture (Angular 17 Clean Architecture)](#4-frontend-architecture)
5. [Cross-Cutting Patterns](#5-cross-cutting-patterns)
6. [Data Flow](#6-data-flow)
7. [Implementation Phases](#7-implementation-phases)
8. [Conventions & Rules](#8-conventions--rules)
9. [API Reference](#9-api-reference)
10. [Key Decisions & Rationale](#10-key-decisions--rationale)

---

## 1. Project Overview

**Eventiy** — An event ticketing platform where users can browse, discover, and book tickets for events. Organizers can create and manage events, ticket types, and photos. Built with DDD, CQRS, and Clean Architecture on the backend, and a layered Clean Architecture on the Angular frontend.

- **Backend**: .NET 9, C#, EF Core 9, MediatR, FluentValidation, JWT Bearer auth
- **Frontend**: Angular 17 (standalone components), Tailwind CSS, Signal-based reactivity
- **Database**: SQL Server (via EF Core migrations)
- **Auth**: JWT (access tokens), password hashing with BCrypt-style hasher

---

## 2. Solution Structure

```
Eventiy.sln
├── Domain/                          # Innermost layer — enterprise business rules
│   ├── Aggregates/                  # DDD Aggregates (Event, Booking, User)
│   ├── Common/                      # Base classes, Result pattern, Domain events
│   ├── Primitives/                  # Address, Money, PhoneNumber value objects
│   ├── Errors/                      # Typed error catalogues per aggregate
│   ├── Persistence/Repositories/    # Repository interfaces
│   └── Abstractions/               # Storage, Photo repository interfaces
│
├── Application/                     # Use case orchestration
│   ├── Features/                    # Grouped by domain: Authentication, Bookings, Events
│   │   ├── Commands/               # CQRS commands (Create, Update, Delete, Cancel…)
│   │   └── Queries/                # CQRS queries (Get, List…)
│   ├── Abstractions/                # Interfaces + MediatR pipeline behaviors
│   │   ├── Behaviors/              # LoggingPipeline, ValidationPipeline
│   │   ├── Messaging/              # ICommand, IQuery, IEventHandler markers
│   │   ├── Outbox/                 # Outbox pattern contracts
│   │   ├── Persistence/            # IUnitOfWork, IApplicationReadDbContext
│   │   └── Security/               # ICurrentUserService, IJwtTokenGenerator
│   ├── EventHandlers/              # Domain event → integration handlers
│   ├── Validators/                 # Domain event validators
│   └── DependencyInjection.cs
│
├── Infrastructure/                  # External concerns implementation
│   ├── Authentication/             # JWT generation, password hashing, CurrentUser
│   ├── Persistence/                # EF Core DbContext, migrations, repositories
│   │   ├── Configuration/          # EF entity configurations (Fluent API)
│   │   ├── Outbox/                 # OutboxMessage, EventSerializer, OutboxProcessor
│   │   ├── Repositories/          # IEventRepository → EventRepository, etc.
│   │   └── SeedData/              # DatabaseSeeder with test + production data
│   ├── Messaging/                  # EventMetadataFactory
│   ├── BackgroundJobs/             # OutboxProcessor (background service)
│   ├── Storage/                    # LocalFileStorageService for EventPhoto uploads
│   ├── Payments/                   # StripePaymentGateway (stub)
│   ├── Migrations/                 # EF Core migrations
│   └── DependencyInjection.cs
│
├── Eventy.WebApi/                   # API host (Presentation layer)
│   ├── Controllers/               # API endpoints (Event, Booking, Auth)
│   ├── Middlewares/                # CorrelationId, GlobalExceptionHandling
│   ├── Extensions/                # Result → ActionResult mapping
│   ├── RequestsDesign/            # Request DTOs (e.g. AddTicketTypeRequest)
│   ├── wwwroot/uploads/           # Uploaded event photos
│   ├── Program.cs                 # Minimal API host setup
│   └── appsettings.json
│
└── EventiyApp/                     # Angular 17 frontend
    └── src/app/
        ├── core/                   # Domain models, enums, mappers, guards
        │   ├── models/            # TypeScript types (Event, Booking, Auth, Result)
        │   ├── enums/             # EventStatus, BookingStatus, UserRole
        │   ├── mappers/           # Backend DTO → frontend model transformers
        │   └── guards/            # Route guards (AuthGuard, RoleGuard)
        ├── application/           # Use cases & HTTP
        │   ├── http/              # Pure HTTP services (HttpClientBase + 4 services)
        │   └── services/          # Application services (orchestration, caching)
        ├── infrastructure/        # Cross-cutting tools
        │   ├── interceptors/      # HttpInterceptorFn (auth, error→toast)
        │   ├── directives/        # ImgFallbackDirective
        │   ├── services/          # ToastService (signal-based)
        │   └── storage/           # SessionStorageService
        ├── shared/                # Reusable UI components
        │   ├── components/        # navbar, event-card, lightbox, photo-uploader,
        │   │                     #   result-toast, search-bar, skeleton-loader
        │   └── pipes/             # DateFormatPipe
        └── features/              # Feature components (page-level)
            ├── home/              # Hero section, categories, events-grid
            ├── auth/              # login, register
            ├── events/            # event-list, event-create, event-detail
            ├── dashboard/         # organizer-dashboard, attendee-dashboard
            └── errors/            # unauthorized, not-found
```

---

## 3. Backend Architecture

### 3.1 Layered Clean Architecture

```
┌──────────────────────────────────────────────────┐
│  Eventy.WebApi (Presentation)                    │
│  Controllers → Middlewares → Program.cs          │
├──────────────────────────────────────────────────┤
│  Application (Use Cases)                         │
│  Handlers → Behaviors → Interfaces               │
├──────────────────────────────────────────────────┤
│  Domain (Enterprise Business Rules)              │
│  Aggregates → ValueObjects → Events → Errors     │
├──────────────────────────────────────────────────┤
│  Infrastructure (External Concerns)              │
│  EF Core → JWT → File Storage → Outbox           │
└──────────────────────────────────────────────────┘
```

**Dependency rule**: Dependencies point inward. Domain has zero dependencies. Application depends on Domain. Infrastructure depends on Application (implements its interfaces). WebApi depends on Infrastructure + Application.

### 3.2 Domain-Driven Design

#### Aggregates

Three aggregates, each with a `AggregateRoot<TId>` base class:

| Aggregate | Root Id | Key Entities | State Machine |
|-----------|---------|-------------|---------------|
| `Event` | `EventId` (Guid VO) | `TicketType[]`, `EventPhoto[]` | Draft → Published → Cancelled / Completed |
| `Booking` | `BookingId` (Guid VO) | — (no sub-entities) | Pending → Confirmed / Cancelled / Refunded / Expired |
| `User` | `UserId` (Guid VO) | — | PendingApproval → Approved (organizers only) |

#### Value Objects (immutable, structural equality)

| VO Class | Properties | Validation |
|----------|-----------|------------|
| `EventName` | `Value: string` | Max 200 chars, not empty |
| `EventId`, `BookingId`, `UserId`, `TicketTypeId`, `EventPhotoId` | `Value: Guid` | Created via `Create(Guid)` with IsFailure check |
| `Address` | `Country, City, Street, PostalCode?` | Not empty fields |
| `Money` | `Amount: decimal, Currency: string` | Positive amount, 3-letter ISO currency |
| `Email` | `Value: string` | Regex validation |
| `Role` | `Value: string` | Must be "Admin", "Organizer", or "Attendee" |
| `PhoneNumber` | `Value: string` | Digits-only validation |

#### Domain Events

Every aggregate state change raises domain events via `DomainEventFactory`. All events extend `DomainEvent` base class and carry `EventMetadata` (correlationId, causationId, userId). Events are persisted via the **Outbox pattern**.

Domain events raised:
- **Event**: Created, Published, Cancelled, Completed, CapacityUpdated, PhotosUpdated
- **TicketType**: Added, Updated, PriceUpdated, CapacityUpdated, Removed, SeatsReserved, SeatsReleased
- **Booking**: Created, Confirmed, Cancelled, CancellationRequested, Expired, Refunded, QuantityUpdated
- **User**: Registered

#### Error Catalog

Typed errors per aggregate in `Domain/Errors/`:
- `EventErrors`, `BookingErrors`, `UserErrors`, `TicketTypeErrors`, `AddressErrors`, `MoneyErrors`

Each returns `Error` records with `Code`, `Message`, and `ErrorType` (Failure, Validation, NotFound, Conflict, Unauthorized).

### 3.3 Result Pattern (No Exceptions for Flow Control)

```csharp
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors { get; }
    public static Result Success();
    public static Result Failure(params Error[] errors);
}

public class Result<TValue> : Result
{
    public TValue Value { get; } // throws if IsFailure
    public static Result<TValue> Success(TValue value);
    public new static Result<TValue> Failure(params Error[] errors);
    public static implicit operator Result<TValue>(TValue? value);
}
```

**Rule**: Every command handler returns `Result` or `Result<TResponse>`. Queries return `Result<TResponse>`. Exceptions are reserved for truly exceptional situations (handled by `GlobalExceptionHandlingMiddleware`).

### 3.4 CQRS with MediatR

- **Commands**: `ICommand : IRequest<Result>` and `ICommand<TResponse> : IRequest<Result<TResponse>>`
- **Queries**: `IQuery<TResponse> : IRequest<Result<TResponse>>`
- **Handlers**: `ICommandHandler<TCommand> : IRequestHandler<TCommand, Result>` (and generic variant)
- **Pipeline behaviors** (registered in `Application/DependencyInjection.cs`):
  1. `LoggingPipelineBehavior<TRequest, TResponse>` — logs start/end + elapsed ms
  2. `ValidationPipelineBehavior<TRequest, TResponse>` — runs all `IValidator<TRequest>` instances before handler; returns `Result.Failure(errors)` on validation failure

### 3.5 MediatR Pipeline Order

```
Request → LoggingPipeline → ValidationPipeline → Command/Query Handler → Result
```

### 3.6 Event Metadata & Causation Chain

Every domain event carries:
- **CorrelationId**: Assigned by `CorrelationIdMiddleware` from `X-Correlation-Id` header (or new GUID). Stored in `HttpContext.Items["CorrelationId"]`.
- **CausationId**: New GUID per event, stored in `EventMetadata.CausationId`.
- **UserId**: From `ICurrentUserService.GetCurrentUserId()` (JWT claim), stored in `EventMetadata.CreatedBy`.
- **Version**: Version number (default 1).

`IEventMetadataFactory` (registered as scoped) reads `IHttpContextAccessor` and `ICurrentUserService` to build the metadata. Every command handler receives `IEventMetadataFactory` via DI.

### 3.7 Outbox Pattern

Domain events raised during command execution are NOT sent directly. Instead:
1. `UnitOfWork.CommitAsync()` collects domain events from tracked aggregates
2. Events are serialized (JSON) via `EventSerializer` and stored in `OutboxMessages` table
3. `OutboxProcessor` (background service, runs every 2 seconds) polls for unprocessed messages
4. Publishes events to MediatR notification handlers (e.g., `BookingCreatedEventHandler`)
5. Marks messages as processed (with lock to prevent double-processing)

Lock atomicity: Uses `BeginTransactionAsync` + `ProcessingLockedAt` timestamp for distributed safety.

### 3.8 EF Core Configuration

- `ApplicationDbContext` — CQRS write context (tracks changes)
- `ReadDbContextAdapter` — Lightweight read-only adapter (no change tracking, `AsNoTracking()` for all queries)
- Entity configurations in `Infrastructure/Persistence/Configuration/` using Fluent API
- Migrations in `Infrastructure/Migrations/`

### 3.9 Authentication

- **Password hashing**: `IPasswordHasher` → `PasswordHasher` (PBKDF2-style with salt)
- **JWT generation**: `IJwtTokenGenerator` → `JwtTokenGenerator` (configurable via `JwtSettings`)
- **Current user resolution**: `ICurrentUserService` → `CurrentUserService` (reads JWT claims from `IHttpContextAccessor`)
- **Authorization**: `[Authorize]` attributes + role checks in handlers

### 3.10 Global Exception Handling Middleware

```csharp
GlobalExceptionHandlingMiddleware — catches:
├── DbUpdateConcurrencyException → 409 Conflict (ProblemDetails)
├── DbUpdateException → 500 (ProblemDetails)
├── OperationCanceledException → 499 Client Closed Request
└── Exception → 500 (ProblemDetails, no stack trace)
```

---

## 4. Frontend Architecture

### 4.1 Clean Architecture Layers (Angular 17)

```
core/               — Pure domain: models, enums, mappers, guards
  ├── models/       — TypeScript interfaces (Event, Booking, Auth, Result)
  ├── enums/        — EventStatus, BookingStatus, UserRole
  ├── mappers/      — Backend DTO → frontend model functions
  └── guards/       — AuthGuard (isAuthenticated), RoleGuard (hasRole)

application/        — Use cases: HTTP contracts + orchestration services
  ├── http/         — HttpClientBase + 4 HTTP services (pure fetch, no mapping)
  └── services/     — Application services (map + cache + orchestrate)

infrastructure/     — Cross-cutting: directives, interceptors, storage, toast
  ├── directives/   — ImgFallbackDirective (@HostListener error)
  ├── interceptors/ — auth.interceptor (JWT), error.interceptor (toast)
  ├── services/     — ToastService (signal-based queue)
  └── storage/      — SessionStorageService

presentation/       — UI components
  ├── features/     — Page-level: home, auth, events, dashboard, errors
  └── shared/       — Reusable: navbar, event-card, lightbox, etc.
```

**Dependency rule**: `core/ → application/ → infrastructure/ → presentation/`. Lower layers never import from higher layers.

### 4.2 HttpClientBase Pattern

All HTTP services extend `HttpClientBase`:

```typescript
abstract class HttpClientBase {
  protected http: HttpClient;

  protected toErrorResult<T>(source: Observable<T>): Observable<Result<T>> {
    return source.pipe(
      map(value => ({ isSuccess: true, isFailure: false, value } as Result<T>)),
      catchError(err => {
        const problem = err.error;
        const errors = problem?.['errors'] ?? problem?.['error']
          ? [{ code: 'validation', message: problem.detail || problem.title }]
          : [{ code: 'network', message: 'Unable to reach the server.' }];
        return of({ isSuccess: false, isFailure: true, errors });
      }),
    );
  }
}
```

### 4.3 Three-Layer Service Architecture

```
HTTP Service (pure)           Application Service (orchestration)     Component
  EventHttpService ──────────→ EventApplicationService ─────────────→ event-list
  BookingHttpService ────────→ BookingApplicationService ───────────→ attendee-dashboard
  AuthHttpService ───────────→ AuthApplicationService ──────────────→ login/register
  EventPhotoHttpService ─────→ (direct or via EventApplicationService)
```

- **HTTP services**: Pure HTTP calls, return `Observable<Result<BackendDto>>`. No mapping, no caching.
- **Application services**: Inject HTTP services, map DTOs to frontend models via `core/mappers/`, add caching where appropriate (e.g., `getEvents()` uses `shareReplay(1)` with manual invalidation).

### 4.4 Signal-Based Toast Service

```typescript
@Injectable({ providedIn: 'root' })
class ToastService {
  readonly toasts = signal<Toast[]>([]);
  showError(msg: string): void;  // adds { type: 'error', message }
  showSuccess(msg: string): void;
  showInfo(msg: string): void;
  dismiss(id: number): void;
}
```

Rendered by `ResultToastComponent` in a fixed overlay. Auto-dismisses after 4 seconds.

### 4.5 Interceptors

| Interceptor | Purpose |
|-------------|---------|
| `auth.interceptor` | Attaches `Authorization: Bearer {token}` to outgoing requests (skips `/auth/` endpoints) |
| `error.interceptor` | Catches `HttpErrorResponse`, maps status codes to `toastService.showError()` messages, handles 401 (auto-logout), 403 (redirect to /unauthorized) |

### 4.6 Guards

| Guard | Logic | Used By |
|-------|-------|---------|
| `AuthGuard` (canActivate) | `auth.isAuthenticated()` | Routes that require login |
| `RoleGuard` (canActivate) | `auth.userRole() === requiredRole` | Organizer dashboard, Admin routes |

### 4.7 Frontend Caching Strategy

- **Event list**: `EventApplicationService.getEvents()` uses `shareReplay(1)` to cache. Manual refresh via `forceRefresh: true` parameter. Cache invalidated on error.
- **Event detail**: No caching (always fetched per navigation).
- **Auth state**: `AuthApplicationService` keeps `currentUser` signal, `isAuthenticated` computed signal, persisted to `SessionStorageService`.

---

## 5. Cross-Cutting Patterns

### 5.1 Full Request Lifecycle

```
Browser ──→ Angular (app) ──→ AuthInterceptor ──→ ErrorInterceptor ──→ HTTP ──→
    CorrelationIdMiddleware ──→ GlobalExceptionHandlingMiddleware ──→ Controller ──→
    MediatR Pipeline ──→ ValidationBehavior ──→ LoggingBehavior ──→ Handler ──→
    Repository ──→ UnitOfWork ──→ Outbox ──→ Response ──→ Client
```

### 5.2 Correlation & Causation Chain

```
Client Request
  └─ X-Correlation-Id (optional, auto-generated if missing)
      └─ CorrelationIdMiddleware stores in HttpContext.Items
          └─ EventMetadataFactory.Create() reads CorrelationId + UserId
              └─ EventMetadata passed to every domain event
                  └─ Outbox stores events with metadata
                      └─ OutboxProcessor publishes events with causation
```

### 5.3 Error Flow

```
Command/Query → ValidationPipeline (fluent validation errors)
  → Command/Query Handler (domain errors from Result.Failure)
    → Controller (Result pattern → ProblemDetails)
      → GlobalExceptionHandlingMiddleware (unhandled exceptions → ProblemDetails)
        → ErrorInterceptor (HTTP errors → ToastService)
          → Component (Result.isFailure → showError toast)
```

---

## 6. Data Flow

### 6.1 Create Event Flow

```
User fills form → EventCreateComponent
  → EventApplicationService.createEvent(data)
    → EventHttpService.createEvent(data) [POST /api/event]
      → EventController.Create(CreateEventCommand, ...)
        → MediatR.Send(CreateEventCommand)
          → LoggingPipelineBehavior → ValidationPipelineBehavior
            → CreateEventCommandHandler
              → Address.Create(…) — domain validation
              → Event.Create(…) — aggregate factory
                → EventCreatedEvent raised (via DomainEventFactory)
              → _eventRepository.AddEventAsync(…)
              → _unitOfWork.CommitAsync()
                → Domain events collected → serialized → OutboxMessages
                → EF Core SaveChangesAsync
              → Result<Guid>.Success(eventId)
          → CreatedAtAction(nameof(GetById), new { id })
        ← 201 Created { id, name, … }
```

### 6.2 Book Ticket Flow

```
User clicks "Book Now" → EventDetailComponent
  → BookingApplicationService.createBooking({ eventId, ticketTypeId, quantity })
    → BookingHttpService.createBooking(data) [POST /api/booking]
      → BookingController.Create(CreateBookingCommand)
        → CreateBookingCommandHandler
          → _eventRepository.GetByIdAsync(eventId)
          → event.ReserveSeats(ticketTypeId, quantity, metadata)
            → TicketType.ReserveSeats(quantity) — validates availability
            → TicketTypeSeatsReservedEvent raised
          → Booking.Create(userId, eventId, ...) — aggregate factory
            → BookingCreatedEvent raised
          → _bookingRepository.AddBookingAsync(booking)
          → _unitOfWork.CommitAsync()
            → Outbox: BookingCreatedEvent + TicketTypeSeatsReservedEvent
          → Result<Guid>.Success(bookingId)
        ← 200 OK { bookingId }
```

### 6.3 Cancel Booking Flow

```
Organizer/Admin clicks "Cancel Booking" → OrganizerDashboardComponent
  → BookingApplicationService.cancelBooking(id)
    → BookingHttpService.cancelBooking(id) [PUT /api/booking/{id}/cancel]
      → BookingController.CancelBooking(Guid id) — param matches route {id}
        → CancelBookingCommandHandler
          → _bookingRepository.GetByIdAsync(id)
          → booking.Cancel(metadata)
            → BookingCancelledEvent raised
            → TicketTypeSeatsReleasedEvent raised (via Event aggregate)
          → _unitOfWork.CommitAsync()
          → Result.Success()
        ← 200 OK
```

### 6.4 Upload Photo Flow

```
Organizer selects files → PhotoUploaderComponent
  → validates: max 10 files, image types, 5MB each
  → EventApplicationService.uploadPhotos(eventId, files)
    → EventPhotoHttpService.uploadPhotos(eventId, files) [POST /api/event/{id}/photos]
      → EventController.UploadPhotos
        → UploadEventPhotosCommandHandler
          → validates files, reads metadata (Exif)
          → _fileStorageService.SaveAsync(file) → returns PublicUrl
          → EventPhoto entity created per file
          → event.AddPhoto(photo) — EventPhotosUpdatedEvent raised
          → _unitOfWork.CommitAsync()
          → Result<EventPhotoResponse[]>.Success(photos)
        ← 200 OK { photos: [...] }
```

---

## 7. Implementation Phases

The project was built in phases. Each phase's characteristics:

### Phase 1: Domain Foundation
**What**: Domain model, result pattern, base classes, value objects, error catalogues, aggregate roots with domain events.

**Key decisions**:
- Result pattern over exceptions for business logic failures
- Value objects validated at construction via static `Create()` factory methods
- Domain events carry causation chain metadata

### Phase 2: Application Layer + CQRS
**What**: MediatR setup, command/query interfaces, handlers, pipeline behaviors (validation + logging), FluentValidation.

**Key decisions**:
- Separate `ICommand<TResponse>` from `IQuery<TResponse>` for read/write segregation
- Validation happens before handler via pipeline behavior
- Handlers receive `IUnitOfWork` for atomic commits

### Phase 3: Infrastructure + Persistence
**What**: EF Core DbContext, repository implementations, migrations, outbox pattern, file storage, JWT auth.

**Key decisions**:
- `ReadDbContextAdapter` for query-only access (no change tracking)
- Outbox pattern for reliable domain event publishing
- `LocalFileStorageService` stores photos to `wwwroot/uploads/`
- Repository pattern abstracts EF Core from Application layer

### Phase 4: API + Middleware
**What**: ASP.NET controllers, `CorrelationIdMiddleware`, `GlobalExceptionHandlingMiddleware`, `ResultExtensions` for `ActionResult` mapping.

**Key decisions**:
- Correlation ID propagated via `HttpContext.Items` (not custom claim/header) to avoid cross-project dependency
- Middleware order: CorrelationId → ExceptionHandling → StaticFiles → HttpsRedirection → CORS → Auth → Controllers
- ProblemDetails RFC 7807 for all error responses

### Phase 5: Backend Fixes (Audit R1-R7, N1-N8)
**What**: Global exception middleware, EF Core removal from Application.csproj, async query methods in read context, dead code removal, structured logging, outbox lock atomicity, capacity property unification, null-check fixes, async anti-pattern fix, migrations.

### Phase 6: Angular Frontend Foundation
**What**: Standalone Angular 17 app, Tailwind CSS, routing, layout, shared components.

**Key decisions**:
- Standalone components (no NgModules)
- Tailwind CSS utility-first styling
- Functional interceptors over class-based

### Phase 7: Frontend Services + Data Layer
**What**: HttpClientBase, HTTP services, application services, mappers, caching, session storage.

### Phase 8: Feature Components
**What**: Home page, event list/detail/create, auth (login/register), dashboards (organizer/attendee), error pages.

### Phase 9: Frontend Fixes & Restructuring (Audit CRIT-01 through MED-01)
**What**: API route mismatch fixes, pagination, photo service fixes, CancelBooking POST→PUT, ImgFallbackDirective, @let template cleanup, event metadata factory, full 4-layer Clean Architecture restructuring.

---

## 8. Conventions & Rules

### General
- No exceptions for business flow control — use Result pattern
- Every command handler injects `IEventMetadataFactory`
- Every domain event carries `EventMetadata`
- No cross-project references from WebApi to Infrastructure (via DI registration only)

### C# / Backend
- Namespace: `Domain.*`, `Application.*`, `Infrastructure.*`, `Eventy.WebApi.*`
- File-scoped namespaces (no braces)
- Primary constructors where possible
- `Result<T>.Failure(errors)` for failure paths
- `Error.Validation(code, message)` for validation errors
- All entity configurations in `Infrastructure/Persistence/Configuration/`
- Commands use `sealed record`; Handlers use `internal class`
- Validators use `internal sealed class` inheriting `AbstractValidator<T>`

### TypeScript / Frontend
- Standalone components with `ChangeDetectionStrategy.OnPush`
- `inject()` for DI (no constructor injection)
- `signal()` for component state, `computed()` for derived state
- `@let` for template variable binding (Angular 17+)
- `@if`/`@for` over `*ngIf`/`*ngFor` except in `*ngTemplateOutlet` scenarios
- `CommonModule` imported only when using structural directives (`*ngIf`, `*ngFor`, `*ngSwitch`) or pipes (`currency`, `date`, `async`, `json`)
- Interfaces over classes for data models
- `Result<T>` discriminated union: `{ isSuccess: true; isFailure: false; value: T } | { isSuccess: false; isFailure: true; errors: Error[] }`
- Mappers are pure functions in `core/mappers/`
- HTTP services return `Result<BackendDto>` (no mapping)
- Application services return `Result<FrontendModel>` (after mapping)

### Git
- Conventional commits: `fix:`, `feat:`, `refactor:`, `docs:`, `chore:`
- Build must pass (0 errors) before commit

---

## 9. API Reference

### Events — `api/event`

| Method | Path | Auth | Request | Response |
|--------|------|------|---------|----------|
| GET | `{id}` | — | — | `EventDetailsResponse` |
| GET | `?page={n}&pageSize={n}` | — | Query | `PaginatedEventResponse { items, totalCount, page, pageSize, totalPages }` |
| POST | `` | Organizer,Admin | `CreateEventCommand` JSON | `201 Created { id }` |
| PUT | `{eventId}/ticket-types` | Organizer,Admin | `AddTicketTypeRequest` JSON | `200 OK { success }` |
| PUT | `{id}` | Organizer,Admin | `UpdateEventCommand` JSON | `200 OK { success }` |
| DELETE | `{id}` | Organizer,Admin | — | `200 OK { success }` |
| GET | `{id}/photos` | — | — | `EventPhotoResponse[]` |
| POST | `{id}/photos` | Organizer,Admin | `multipart/form-data` (files) | `EventPhotoResponse[]` |
| DELETE | `{id}/photos/{photoId}` | Organizer,Admin | — | `200 OK` |
| PUT | `{id}/photos/{photoId}/cover` | Organizer,Admin | — | `200 OK` |
| PUT | `{id}/photos/{photoId}/metadata` | Organizer,Admin | `{ caption?, displayOrder? }` | `200 OK` |
| PUT | `{id}/photos/reorder` | Organizer,Admin | `{ orderedPhotoIds: string[] }` | `200 OK` |

### Bookings — `api/booking`

| Method | Path | Auth | Request | Response |
|--------|------|------|---------|----------|
| GET | `{id}` | Any Auth | — | `BookingDetailsResponse` |
| GET | `event/{eventId}` | Any Auth | — | `BookingByEventResponse[]` |
| POST | `` | Any Auth | `CreateBookingCommand` JSON | `200 OK { bookingId }` |
| POST | `{bookingId}/confirm` | Any Auth | — | `200 OK { success }` |
| PUT | `{id}/cancel` | Any Auth | — | `200 OK { success }` |
| GET | `my` | Any Auth | — | `BookingByUserResponse[]` |

### Auth — `api/auth`

| Method | Path | Auth | Request | Response |
|--------|------|------|---------|----------|
| POST | `register` | — | `RegisterUserCommand` JSON | `AuthResponse { token, email, role }` |
| POST | `login` | — | `LoginCommand` JSON | `AuthResponse { token, email, role }` |
| POST | `organizers/{userId}/approve` | Admin | — | `200 OK { success }` |

---

## 10. Key Decisions & Rationale

| Decision | Rationale |
|----------|-----------|
| **Result pattern over exceptions** | Business rule violations are expected, not exceptional. Result type makes failure paths explicit at the type level. |
| **Outbox pattern** | Ensures at-least-once delivery of domain events without 2PC. Decouples event publishing from the transaction. |
| **ReadDbContextAdapter** | Query handlers should never accidentally modify data. Separate read context enforces `AsNoTracking()` by default and avoids change tracker overhead. |
| **CorrelationId in HttpContext.Items** | Avoids forcing WebApi to reference Infrastructure. Middleware stores the ID; EventMetadataFactory (in Infrastructure) reads it via `IHttpContextAccessor`. |
| **Two-layer HTTP + Application services (frontend)** | Separation of concerns: HTTP services are pure fetch (testable, swapable). Application services add caching, mapping, orchestration. |
| **shareReplay(1) caching** | Event list is read-heavy, write-infrequent. Caching with manual invalidation matches the backend query idempotency pattern without stale data risk. |
| **Signal-based ToastService** | Avoids component coupling. Any service or component can show toasts by injecting `ToastService`. The `ResultToastComponent` reactively renders the queue. |
| **Standalone components (no NgModules)** | Angular 17 best practice. Reduces boilerplate, enables lazy loading per route without shared modules. |
| **Tailwind CSS utility classes** | Zero runtime CSS, small bundle (purged unused), consistent design tokens (primary/secondary/text colors via `tailwind.config.js`). |
| **FluentValidation over data annotations** | Separation of concerns — validation rules live in Application layer, not on Domain models. Pipeline behavior runs validators automatically. |
| **Aggregate-per-file with nested types** | Each aggregate root, entity, value object, enum, and event has its own file. No monolithic files. Ensures clear boundaries and testability. |
