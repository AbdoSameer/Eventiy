# Eventiy

Eventiy is a full-stack event ticketing platform for browsing, creating, managing, reserving, and booking tickets for events such as matches, parties, conferences, concerts, theater shows, food events, education events, outdoor gatherings, and other live experiences.

The backend is built with .NET 9 using Clean Architecture, Domain-Driven Design, CQRS, MediatR, FluentValidation, EF Core 9, SQL Server, the Result pattern, JWT authentication with refresh tokens, Redis cache-aside, Redis Pub/Sub, WebSockets, Stripe Checkout, local file storage, and the Transactional Outbox pattern. The frontend is an Angular 19 standalone-component app that uses signals, lazy routes, layered frontend Clean Architecture, D3 seating-map rendering, HTTP/application services, guards, interceptors, and Tailwind CSS.

For the detailed architecture specification and workflow trace, see [`EVENTIY_ARCHITECTURE.md`](EVENTIY_ARCHITECTURE.md).

## Contents

- [Features](#features)
- [Architecture at a glance](#architecture-at-a-glance)
- [Solution structure](#solution-structure)
- [Backend design](#backend-design)
- [Frontend design](#frontend-design)
- [API overview](#api-overview)
- [Tech stack](#tech-stack)
- [Getting started](#getting-started)
- [Configuration](#configuration)
- [Database and seed data](#database-and-seed-data)
- [Running the application](#running-the-application)
- [Development commands](#development-commands)
- [Testing](#testing)
- [Project conventions](#project-conventions)
- [Documentation](#documentation)

## Features

### Event discovery and management

- Browse paginated events.
- Filter events by event type/category: `Music`, `Tech`, `Sports`, `Art`, `Food`, `Education`, `Theater`, and `Outdoors`.
- Find nearby events with latitude, longitude, and distance query parameters.
- View event details, ticket types, lowest ticket price, sold/available capacity, location, cover photo, and photo gallery.
- Create, update, publish, cancel, and manage events as an organizer or admin.
- Use admin override endpoints for event updates, ticket-type creation, and event publication.
- Upload, delete, reorder, update metadata for, and set cover photos for events.
- Store event photos locally under the API `wwwroot` upload directory.

### Ticketing, seating, and booking

- Add ticket types with name, price, currency, and capacity.
- Track ticket inventory through reserved, sold, available, and unavailable counts.
- Reserve seats atomically when a booking is created.
- Confirm reservations and move seats from reserved to sold.
- Release reserved seats on pending-booking cancellation or expiration.
- Refund sold seats when confirmed bookings are cancelled/refunded.
- Support pending, confirmed, cancelled, expired, and refunded booking states.
- Support attendee self-cancellation for allowed bookings and admin/organizer booking management.
- View bookings by current user, by event, and through admin all-bookings endpoints.
- Show booking details at `/bookings/:id` in the Angular app.

### Payments

- Support `Instant` payments through Stripe Checkout.
- Support `Deferred` payments with Fawry-style reference codes such as `FAW-XXXXXXXX`.
- Store refreshable payment/booking hold windows: short holds for instant payments and longer holds for deferred payments.
- Initiate instant payment before persisting a booking so failed payment setup does not leave reserved seats behind.
- Compensate failed commits by cancelling orphaned Stripe checkout sessions.
- Confirm Stripe payments through a signed webhook endpoint.
- Confirm deferred payments through a backend command using the generated reference code.
- Use `MockPaymentGateway` for local development when `Stripe:UseMock=true`.

### Real-time seat selection

- Use a D3-powered Angular seating chart for venue/section/seat selection.
- Lock and unlock seats over WebSockets at `/ws/venues/{eventId}`.
- Broadcast seat-state deltas across API instances with Redis Pub/Sub channels named `seats:event:{eventId}`.
- Handle collision responses when another user reserves a seat first.
- Use frontend signals for selected seats, pending locks, connection status, deltas, and reconnection state.
- Navigate from the seating chart to the created booking after checkout.

### Authentication and roles

- Register and log in with JWT bearer authentication.
- Issue refresh tokens, store them as HttpOnly secure cookies, and rotate them through `/api/auth/refresh`.
- Revoke refresh tokens through `/api/auth/revoke`.
- Support `Admin`, `Organizer`, and `Attendee` roles.
- Require organizer approval before organizer accounts can fully operate.
- Protect backend endpoints with role-based `[Authorize]` rules.
- Protect Angular routes with auth and role guards.
- Store frontend auth state in `sessionStorage` instead of `localStorage`.

### Reliability and cross-cutting behavior

- Return expected business and validation failures through `Result` and typed `Error` values.
- Run FluentValidation through a MediatR validation pipeline.
- Run request/use-case logging through a MediatR logging pipeline.
- Map unhandled exceptions to RFC 7807-style `ProblemDetails` responses.
- Add and propagate correlation IDs through `X-Correlation-Id`.
- Persist domain events using the Transactional Outbox pattern in the same database transaction as aggregate changes.
- Process outbox messages with a background worker, retries, exponential backoff, and dead-letter storage.
- Requeue dead-letter outbox messages through admin endpoints.
- Guard domain-event consumers and Stripe webhooks with idempotency records.
- Use SQL Server `rowversion` optimistic concurrency plus retry loops for high-contention booking flows.
- Expire stale pending bookings and release seats in a background job.
- Reconcile expired instant bookings by cancelling orphaned Stripe sessions in a background job.
- Use Redis cache-aside for event list, event details, and event photos with graceful degradation if Redis is unavailable.

## Architecture at a glance

```text
Browser / Angular 19 UI
        |
        v
Angular application services, signals, guards, interceptors, D3 seating chart
        |
        v
Eventy.WebApi controllers, middleware, HTTP API, WebSocket gateway
        |
        v
MediatR commands, queries, validators, pipeline behaviors
        |
        v
Domain aggregates, entities, value objects, domain events, Result objects
        |
        v
Infrastructure: EF Core, SQL Server, Redis, Stripe, JWT, file storage, outbox, jobs
```

Dependency direction follows Clean Architecture:

```text
Eventy.WebApi  -> Application -> Domain
Eventy.WebApi  -> Infrastructure -> Application -> Domain
Infrastructure -> Application -> Domain
EventiyApp     -> Eventy.WebApi HTTP/WebSocket API
```

The `Domain` project is the innermost layer. It has no ASP.NET Core, EF Core, MediatR, Redis, Stripe, or Angular dependency. Application code owns use-case orchestration; Infrastructure owns external I/O.

## Solution structure

```text
Eventiy.sln
├── Domain/                         # Pure DDD model, Result, typed errors, domain events
├── Application/                    # CQRS commands/queries, handlers, validators, abstractions
├── Infrastructure/                 # EF Core, auth, caching, payments, real-time, outbox, jobs
├── Eventy.WebApi/                  # ASP.NET Core API, controllers, middleware, OpenAPI/Scalar
├── EventiyApp/                     # Angular 19 frontend
├── tests/                          # Domain, application, integration, concurrency test projects
├── EVENTIY_ARCHITECTURE.md         # Enterprise architecture specification and workflow trace
├── ABOUT.md                        # Short project overview
└── README.md                       # Project introduction and setup guide
```

### Backend projects

| Project | Responsibility |
| --- | --- |
| `Domain` | Enterprise business rules: `Event`, `TicketType`, `EventPhoto`, `Booking`, `User`, `RefreshToken`, value objects, typed errors, domain events, and `Result` types. |
| `Application` | Use cases: MediatR commands/queries, handlers, FluentValidation validators, pipeline behaviors, repository/security/cache/payment/outbox abstractions. |
| `Infrastructure` | External details: EF Core `ApplicationDbContext`, migrations, repositories, JWT, BCrypt password hashing, Redis caching, Redis Pub/Sub, Stripe/mock payments, local file storage, outbox processing, background jobs, seed data. |
| `Eventy.WebApi` | Presentation layer: controllers, middleware, CORS, JSON options, OpenAPI, Scalar API reference, static file serving, WebSocket gateway. |

### Frontend project

| Folder | Responsibility |
| --- | --- |
| `EventiyApp/src/app/core` | Frontend domain models, enums, mappers, and route guards. |
| `EventiyApp/src/app/application/http` | HTTP services that call backend APIs and return frontend `Result<T>` values. |
| `EventiyApp/src/app/application/services` | Use-case orchestration, mapping, caching, auth session state, booking/event workflows, seating services. |
| `EventiyApp/src/app/infrastructure` | Interceptors, directives, toast service, and session storage. |
| `EventiyApp/src/app/features` | Page-level features: home, auth, events, dashboards, admin outbox, booking detail, D3 seating chart. |
| `EventiyApp/src/app/shared` | Reusable UI components such as cards, navbar, footer, photo uploader, lightbox, skeleton loader, and pipes. |
| `EventiyApp/src/app/presentation` | Error pages such as unauthorized and not-found screens. |

## Backend design

### Domain-Driven Design

The domain model centers on these aggregates and entities:

- `Event`: event lifecycle, type/category, date, location, capacity, ticket types, photos, publication/cancellation/completion state, and row-version concurrency token.
- `TicketType`: ticket name, price, currency, capacity, reserved count, sold count, and availability rules. The application layer also contains venue-layout validation for section-aware ticketing flows.
- `EventPhoto`: uploaded image metadata, public URL, caption, display order, and cover-photo state.
- `Booking`: user reservation, ticket type, quantity, total amount, payment method, reference code, hold expiry, confirmation/cancellation/refund/expiry state, and row-version concurrency token.
- `User`: identity, role, password hash, organizer approval state, refresh tokens, and row-version concurrency token.
- `RefreshToken`: hashed refresh-token lifecycle, expiry, revocation, and replacement tracking.

Important value objects include:

- `EventId`, `TicketTypeId`, `BookingId`, `EventPhotoId`, `UserId`
- `EventName`
- `Address` with optional latitude and longitude
- `Money`
- `Email`
- `Role`

Domain methods return `Result` or `Result<T>` for expected business failures. Domain events are raised directly from aggregates with `new XxxEvent(...)` calls. Domain methods receive `DateTime utcNow` from the Application layer instead of reading system time directly.

### CQRS with MediatR

Application use cases are split into commands and queries:

- Commands implement `ICommand` or `ICommand<TResponse>`.
- Queries implement `IQuery<TResponse>`.
- Handlers return `Result` or `Result<TResponse>`.
- `LoggingPipelineBehavior` records execution details.
- `ValidationPipelineBehavior` runs FluentValidation validators and creates typed Result failures without reflection.

Examples:

- Authentication: `RegisterUserCommand`, `LoginCommand`, `RefreshTokenCommand`, `ApproveOrganizerCommand`
- Events: `CreateEventCommand`, `UpdateEventCommand`, `PublishEventCommand`, `CancelEventCommand`, `AddTicketTypeCommand`, photo commands
- Bookings: `CreateBookingCommand`, `ConfirmBookingCommand`, `ConfirmBookingFromWebhookCommand`, `ConfirmDeferredPaymentCommand`, `CancelBookingCommand`
- Queries: `GetEventsQuery`, `GetEventDetailsQuery`, `GetEventPhotosQuery`, `GetAllBookingsQuery`, `GetBookingsByUserQuery`, `GetBookingDetailsQuery`, `GetBookingByEventQuery`

### Persistence and outbox

Infrastructure uses EF Core 9 with SQL Server:

- `ApplicationDbContext` for writes and outbox persistence.
- `ReadDbContextAdapter` / `IApplicationReadDbContext` for no-tracking read-side queries.
- Repository implementations for events, bookings, users, event photos, outbox messages, and idempotency state.
- SQL Server `rowversion` columns for optimistic concurrency.
- Fluent API configurations in `Infrastructure/Persistence/Configuration`.
- Migrations in `Infrastructure/Migrations`.

`UnitOfWork.CommitAsync()` extracts domain events from tracked aggregate roots, stores them as outbox messages, and commits aggregate changes plus outbox rows atomically. `OutboxProcessor` polls pending messages, locks batches, dispatches typed domain-event handlers, retries failures, and moves exhausted/non-retryable messages to dead letters.

### Caching

`ICacheService` abstracts cache operations. `RedisCacheService` implements it with StackExchange.Redis and degrades gracefully if Redis is unavailable.

Current cache usage includes:

| Read model | TTL | Key shape |
| --- | --- | --- |
| Event list | 30 seconds | `events:list:{page}:{size}:{type}:{lat}:{lng}:{dist}` |
| Event details | 60 seconds | `event:details:{eventId}` |
| Event photos | 120 seconds | `event:photos:{eventId}` |

Booking queries are intentionally uncached because they contain personal/financial data and mutate frequently. Cache invalidation runs after successful commits.

### Payments and booking reliability

`IPaymentService` has two implementations:

- `StripePaymentGateway` for production Stripe Checkout sessions and session cancellation.
- `MockPaymentGateway` for local development and tests.

Instant booking flow initiates payment before the booking is persisted. If payment initiation fails, no booking or seat reservation is stored. If persistence fails after payment initiation, the handler calls `CancelPaymentAsync` to compensate the orphaned checkout session.

Deferred bookings generate a Fawry-style reference code and can be confirmed with `ConfirmDeferredPaymentCommand`.

### Real-time seat synchronization

The API exposes a WebSocket gateway at:

```text
/ws/venues/{eventId}?token={jwt}
```

The WebSocket middleware:

- Accepts venue-scoped connections.
- Authenticates the user from a query-string JWT.
- Sends `CONNECTED`, `ACK`, `DELTA`, `COLLISION`, and heartbeat messages.
- Applies LOCK/UNLOCK seat messages against the `Event` aggregate.
- Uses Redis Pub/Sub to broadcast seat-state changes across instances.

### Background jobs

| Job | Interval | Purpose |
| --- | --- | --- |
| `OutboxProcessor` | 5 seconds | Polls and dispatches outbox messages. |
| `BookingExpirationJob` | 1 minute | Expires pending bookings past hold time and releases reserved seats. |
| `PaymentReconciliationJob` | 2 minutes | Cancels orphaned Stripe checkout sessions for expired instant bookings. |

### File storage

`LocalFileStorageService` stores uploaded event photos under:

```text
Eventy.WebApi/wwwroot/uploads/events
```

Allowed image types are JPEG, PNG, and WebP. The maximum file size is 5 MB per image.

## Frontend design

The Angular app uses standalone components, lazy routes, Angular signals, and layered frontend Clean Architecture.

### Core layer

`core` contains frontend domain types and pure mapping logic:

- Event, booking, auth, ticket, and result models.
- Event status, booking status, and user role enums.
- Backend DTO to frontend model mappers.
- Auth and role route guards.

### Application layer

The application layer separates HTTP access from orchestration:

- HTTP services include auth, events, event photos, bookings, admin events, and admin outbox.
- Application services map responses, manage cache/session state, expose use-case methods, and coordinate booking/event workflows.
- `EventApplicationService` stores user location, nearby filtering state, and short-lived event-list cache.
- `BookingApplicationService` handles booking creation, cancellation, details, and deferred-payment confirmation.

### Infrastructure layer

Cross-cutting frontend infrastructure includes:

- `auth.interceptor` to attach JWT bearer tokens.
- `error.interceptor` to map HTTP errors to toasts and auth redirects.
- `ToastService` with Angular signals.
- `SessionStorageService` for session-scoped auth state.
- `ImgFallbackDirective` for image fallback behavior.

### D3 seating chart

The seating chart feature uses:

- `VenueViewStore` for selected seats, venue data, filters, computed totals, and server deltas.
- `VenueGraphRendererService` for SVG rendering, zoom/pan, block highlighting, seat updates, and collision flashes.
- `LiveStateSyncService` for WebSocket connection, heartbeat, reconnects, deltas, collisions, and ACK streams.
- `SeatLockOrchestratorService` for optimistic seat locking and collision rollback.
- `SeatingChartComponent` to wire store, renderer, live sync, checkout, and booking creation.

### Presentation and routes

The frontend includes lazy routes for:

- `/` — home
- `/login` — login
- `/register` — registration
- `/events` — event listing
- `/events/create` — organizer/admin event creation
- `/events/:id` — event details
- `/events/:id/edit` — organizer/admin event editing
- `/events/:id/seats` — seating chart and seat selection
- `/dashboard/organizer` — organizer dashboard
- `/dashboard/attendee` — attendee bookings dashboard
- `/bookings/:id` — booking details
- `/admin/outbox/dead-letters` — admin dead-letter management
- `/unauthorized` — unauthorized page
- wildcard not-found route

The Angular development server runs on port `57354` and proxies API requests through `proxy.conf.json`.

## API overview

Controller base paths use ASP.NET Core `[Route("api/[controller]")]`, so routes are shown lowercase for consistency. ASP.NET Core route matching is case-insensitive by default.

### Events — `/api/event`

| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| `GET` | `/api/event/{id}` | Public | Get event details. |
| `GET` | `/api/event?page=1&pageSize=20&type=Sports&userLatitude=...&userLongitude=...&distanceInKm=20` | Public | Get paginated events with optional type and nearby filters. |
| `POST` | `/api/event` | Organizer/Admin | Create an event. |
| `PUT` | `/api/event/{id}` | Organizer/Admin | Update event details. |
| `POST` | `/api/event/{eventId}/ticket-types` | Organizer/Admin | Add a ticket type. |
| `PUT` | `/api/event/{id}/cancel` | Organizer/Admin | Cancel an event. |
| `GET` | `/api/event/{id}/photos` | Public | Get event photos. |
| `POST` | `/api/event/{id}/photos` | Organizer/Admin | Upload event photos with `multipart/form-data`. |
| `DELETE` | `/api/event/{id}/photos/{photoId}` | Organizer/Admin | Delete an event photo. |
| `PUT` | `/api/event/{id}/photos/{photoId}/cover` | Organizer/Admin | Set the cover photo. |
| `PUT` | `/api/event/{id}/photos/{photoId}/metadata` | Organizer/Admin | Update photo caption/display order. |
| `PUT` | `/api/event/{id}/photos/reorder` | Organizer/Admin | Reorder photos. |

### Bookings — `/api/booking`

| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| `GET` | `/api/booking/{id}` | Any authenticated user | Get booking details. |
| `GET` | `/api/booking/event/{eventId}` | Any authenticated user | Get bookings for an event. |
| `POST` | `/api/booking` | Any authenticated user | Create a booking and optionally initiate payment. |
| `POST` | `/api/booking/{bookingId}/confirm` | Any authenticated user | Confirm a booking. |
| `PUT` | `/api/booking/{id}/cancel` | Any authenticated user | Cancel a booking. |
| `GET` | `/api/booking/my` | Any authenticated user | Get bookings for the current user. |
| `POST` | `/api/booking/confirm-deferred` | Any authenticated user | Confirm deferred payment by reference code. |

### Authentication — `/api/auth`

| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| `POST` | `/api/auth/register` | Public | Register a user and set refresh-token cookie when applicable. |
| `POST` | `/api/auth/login` | Public | Log in, return auth response, and set refresh-token cookie. |
| `POST` | `/api/auth/refresh` | Public | Rotate refresh token and issue a new access token. |
| `POST` | `/api/auth/revoke` | Authenticated | Delete refresh-token cookie and revoke token flow. |
| `POST` | `/api/auth/organizers/{userId}/approve` | Admin | Approve an organizer account. |

### Admin events — `/api/admin/events`

| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| `PUT` | `/api/admin/events/{id}` | Admin | Admin update event. |
| `POST` | `/api/admin/events/{eventId}/ticket-types` | Admin | Admin add ticket type. |
| `POST` | `/api/admin/events/{eventId}/publish` | Admin | Admin publish event. |

### Admin bookings — `/api/admin/bookings`

| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| `GET` | `/api/admin/bookings?status=Pending` | Admin | List all bookings, optionally filtered by status. |
| `POST` | `/api/admin/bookings/{bookingId}/confirm` | Admin | Admin confirm booking. |
| `PUT` | `/api/admin/bookings/{bookingId}/cancel` | Admin | Admin cancel booking. |

### Admin outbox — `/api/admin/outbox`

| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| `GET` | `/api/admin/outbox/dead-letters` | Admin | List failed outbox messages. |
| `POST` | `/api/admin/outbox/dead-letters/{id}/requeue` | Admin | Requeue a dead-letter message. |

### Stripe webhook — `/api/webhooks/stripe`

| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| `POST` | `/api/webhooks/stripe` | Stripe signature | Verify Stripe signature and confirm paid checkout sessions. |

### WebSocket gateway

| Endpoint | Auth | Description |
| --- | --- | --- |
| `/ws/venues/{eventId}?token={jwt}` | Query-string JWT | Real-time seat lock/unlock and seat-state deltas for one event. |

## Tech stack

### Backend

- .NET 9
- ASP.NET Core Web API
- EF Core 9
- SQL Server 2022-compatible database
- MediatR 14.1.0
- FluentValidation 12.1.1
- JWT Bearer authentication
- BCrypt.Net-Next
- StackExchange.Redis 2.8.31
- Stripe.net 52.1.0
- Scalar API reference

### Frontend

- Angular 19
- Standalone components
- Angular signals
- D3 7.9
- Angular CDK
- RxJS
- Tailwind CSS
- Karma/Jasmine test setup

### Tests

- xUnit
- FluentAssertions
- NSubstitute
- Microsoft.AspNetCore.Mvc.Testing
- Testcontainers.MsSql
- Respawn
- WireMock.Net reference

## Getting started

### Prerequisites

- .NET 9 SDK
- Node.js and npm
- SQL Server or a SQL Server-compatible database
- Optional: Redis on `localhost:6379` for cache and real-time Pub/Sub support
- Optional: EF Core CLI for migrations
- Optional: Stripe CLI for local webhook testing

Install EF Core CLI if needed:

```bash
dotnet tool install --global dotnet-ef
```

### Restore backend packages

```bash
dotnet restore Eventiy.sln
```

### Install frontend packages

```bash
cd EventiyApp
npm install
```

### Build backend

```bash
dotnet build Eventy.WebApi
```

### Build frontend

```bash
cd EventiyApp
npm run build
```

## Configuration

### Backend connection strings

Update `Eventy.WebApi/appsettings.json`, user secrets, or environment variables for your local machine.

Required SQL Server connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=EventiyDB;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

Optional Redis connection string:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

If Redis is unavailable, cache operations log and degrade gracefully. Real-time Pub/Sub requires Redis for multi-instance broadcasting.

### JWT settings

`Infrastructure.DependencyInjection` requires `Jwt:Secret` to exist and be at least 32 characters. The repository intentionally does not store this secret in `appsettings.json`.

For local development, use .NET user secrets:

```bash
dotnet user-secrets set "Jwt:Secret" "replace-with-a-local-development-secret-at-least-32-characters" --project Eventy.WebApi
dotnet user-secrets set "Jwt:Issuer" "Eventiy.Api" --project Eventy.WebApi
dotnet user-secrets set "Jwt:Audience" "Eventiy.Client" --project Eventy.WebApi
dotnet user-secrets set "Jwt:ExpiryMinutes" "1440" --project Eventy.WebApi
```

Do not commit real secrets.

### Stripe settings

For local development without Stripe, enable the mock gateway:

```bash
dotnet user-secrets set "Stripe:UseMock" "true" --project Eventy.WebApi
```

For Stripe Checkout and webhooks, configure real settings through user secrets or environment variables:

```bash
dotnet user-secrets set "Stripe:UseMock" "false" --project Eventy.WebApi
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..." --project Eventy.WebApi
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..." --project Eventy.WebApi
dotnet user-secrets set "Stripe:SuccessUrl" "http://127.0.0.1:57354/payment/success" --project Eventy.WebApi
dotnet user-secrets set "Stripe:CancelUrl" "http://127.0.0.1:57354/payment/cancel" --project Eventy.WebApi
```

When `Stripe:UseMock=false`, the API fails startup if `SecretKey`, `WebhookSecret`, `SuccessUrl`, or `CancelUrl` are missing.

### Frontend API URL

Development frontend API URL:

```text
EventiyApp/src/environments/environment.ts
```

Default development value:

```ts
apiUrl: 'https://localhost:7001/api'
```

Production frontend API URL:

```text
EventiyApp/src/environments/environment.prod.ts
```

Default production value:

```ts
apiUrl: '/api'
```

## Database and seed data

Apply migrations:

```bash
dotnet ef database update --project Infrastructure --startup-project Eventy.WebApi
```

`DatabaseSeederService` runs from the API host, applies migrations, and seeds local/demo data when needed.

Seed data includes:

- Admin user
- Approved and pending organizers
- Multiple attendees
- 18 events across all event types with real coordinates
- Venue-mapped ticket types and realistic pricing
- Bookings using a mix of instant and deferred payment methods

The seed data is for local development and demo workflows.

## Running the application

### Run backend API

```bash
dotnet run --project Eventy.WebApi
```

Development features:

- OpenAPI endpoint mapping
- Scalar API reference
- CORS for `localhost` and `127.0.0.1` on Angular ports `4200` and `57354`
- Static file serving for uploaded event images
- WebSocket endpoint for venue seat updates
- Hosted background jobs for outbox processing, booking expiration, and payment reconciliation

The backend is expected at:

```text
https://localhost:7001
```

### Run frontend app

```bash
cd EventiyApp
npm start
```

The Angular app runs at:

```text
http://127.0.0.1:57354
```

## Development commands

From the repository root:

```bash
# Restore backend dependencies
dotnet restore Eventiy.sln

# Build backend API and referenced projects
dotnet build Eventy.WebApi

# Apply EF Core migrations
dotnet ef database update --project Infrastructure --startup-project Eventy.WebApi

# Run backend
dotnet run --project Eventy.WebApi
```

From `EventiyApp`:

```bash
# Install frontend dependencies
npm install

# Run Angular dev server
npm start

# Build Angular app
npm run build

# Run Angular unit tests
npm test
```

## Testing

The repository includes backend unit, integration, and concurrency tests:

```text
tests/
├── Eventy.Domain.UnitTests/         # Pure domain/value-object tests
├── Eventy.Application.UnitTests/    # Handler tests with mocked dependencies
├── Eventy.IntegrationTests/         # Full HTTP pipeline with Testcontainers SQL Server
├── Eventy.ConcurrencyTests/         # Race-condition and high-contention booking tests
└── Eventy.Testing.Foundation/       # Shared factories, builders, fakes, and test auth
```

Useful commands:

```bash
# Run all .NET tests
dotnet test Eventiy.sln

# Run one test project
dotnet test tests/Eventy.Domain.UnitTests/Eventy.Domain.UnitTests.csproj

# Run Angular unit tests
cd EventiyApp
npm test
```

Integration and concurrency tests use SQL Server containers and may require Docker.

## Example requests

### Register

```http
POST /api/auth/register
Content-Type: application/json

{
  "firstName": "Sara",
  "lastName": "Attendee",
  "email": "sara@example.com",
  "password": "StrongPassword123!",
  "role": "Attendee"
}
```

### Log in

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "sara@example.com",
  "password": "StrongPassword123!"
}
```

### Refresh token

```http
POST /api/auth/refresh
Cookie: refreshToken={refresh-token-cookie}
```

### Create event

```http
POST /api/event
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Championship Match",
  "capacity": 50000,
  "date": "2027-12-20T18:00:00Z",
  "location": {
    "country": "Egypt",
    "city": "Cairo",
    "street": "Stadium Road",
    "latitude": 30.0444,
    "longitude": 31.2357
  },
  "description": "Football championship final",
  "type": "Sports"
}
```

### Add ticket type

```http
POST /api/event/{eventId}/ticket-types
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "VIP",
  "amount": 250,
  "currency": "USD",
  "capacity": 100
}
```

### Create instant booking

```http
POST /api/booking
Authorization: Bearer {token}
Content-Type: application/json

{
  "eventId": "00000000-0000-0000-0000-000000000000",
  "ticketTypeId": "00000000-0000-0000-0000-000000000000",
  "quantity": 2,
  "paymentMethod": "Instant"
}
```

### Confirm deferred payment

```http
POST /api/booking/confirm-deferred
Authorization: Bearer {token}
Content-Type: application/json

{
  "referenceCode": "FAW-1A2B3C4D"
}
```

### Stripe webhook

```http
POST /api/webhooks/stripe
Stripe-Signature: {stripe-signature}
Content-Type: application/json

{ ...checkout.session.completed payload... }
```

## Project conventions

- Domain remains framework-free: no EF Core, ASP.NET Core, MediatR, Redis, Stripe, or infrastructure dependencies.
- Domain must not contain authorization rules; roles are enforced in controllers and application handlers.
- Business validation uses `Result` and typed `Error` values instead of exceptions.
- Domain operations receive `DateTime utcNow` from the Application layer.
- Domain events are raised directly from aggregates and persisted through the outbox.
- Application handlers orchestrate use cases and call repositories through interfaces.
- Commands and queries are mediated through MediatR.
- FluentValidation validators live in the Application layer.
- Cache invalidation should happen after successful commits.
- Payment initiation must avoid orphaned reservations and compensate failed commits.
- Angular HTTP services stay thin; application services map, cache, and orchestrate.
- Angular components should use standalone APIs, signals where appropriate, and route guards for protected pages.

## Documentation

- [`EVENTIY_ARCHITECTURE.md`](EVENTIY_ARCHITECTURE.md): full enterprise architecture specification, pattern matrix, and end-to-end booking workflow trace.
- [`ABOUT.md`](ABOUT.md): short product summary and project vision.
- [`EventiyApp/README.md`](EventiyApp/README.md): Angular CLI generated frontend notes.
