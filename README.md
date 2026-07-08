# Eventiy

Eventiy is a full-stack event ticketing platform for browsing, creating, managing, and booking tickets for events such as matches, parties, conferences, shows, and other gatherings.

The backend is built with .NET 9 using Clean Architecture, Domain-Driven Design, CQRS, MediatR, FluentValidation, EF Core, the Result pattern, JWT authentication, Redis-backed caching, and the Outbox pattern. The frontend is an Angular standalone-component app that follows layered Clean Architecture with signal-based state, HTTP/application services, guards, interceptors, and Tailwind CSS.

For the complete architecture reference, see [`ARCHITECTURE.md`](ARCHITECTURE.md).

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
- [Development commands](#development-commands)
- [Project conventions](#project-conventions)

## Features

### Event discovery and management

- Browse paginated events.
- Filter events by event type/category.
- Find nearby events with latitude, longitude, and distance query parameters.
- View event details, ticket types, location, photos, and availability.
- Create, update, cancel, and manage events as an organizer or admin.
- Upload, delete, reorder, and set cover photos for events.

### Ticketing and booking

- Add ticket types with price, currency, and capacity.
- Reserve seats when a booking is created.
- Confirm bookings and move seats from reserved to sold.
- Cancel bookings and release reserved capacity.
- View bookings by event and by the current user.
- Track booking lifecycle states such as pending, confirmed, cancelled, refunded, and expired.

### Authentication and roles

- Register and log in with JWT authentication.
- Support `Admin`, `Organizer`, and `Attendee` roles.
- Require organizer approval before organizer accounts can fully operate.
- Protect backend endpoints with role-based `[Authorize]` rules.
- Protect Angular routes with auth and role guards.

### Reliability and cross-cutting behavior

- Result-pattern responses for expected business and validation failures.
- FluentValidation command validation through a MediatR pipeline behavior.
- Request logging through a MediatR logging pipeline behavior.
- Global exception handling with RFC 7807-style `ProblemDetails` responses.
- Correlation IDs for request tracing and domain-event metadata.
- Outbox persistence for domain events.
- Dead-letter storage and requeue endpoints for failed outbox messages.
- Idempotency tracking for domain-event handlers.
- Redis cache-aside support for event list and event details queries.

## Architecture at a glance

```text
Browser / Angular UI
        |
        v
EventiyApp application services
        |
        v
Eventy.WebApi controllers and middleware
        |
        v
MediatR commands, queries, validators, and behaviors
        |
        v
Domain aggregates, value objects, domain events, and Result objects
        |
        v
Infrastructure repositories, EF Core, SQL Server, Redis, file storage, JWT, outbox
```

Dependency direction follows Clean Architecture:

```text
Eventy.WebApi  -> Application -> Domain
Infrastructure -> Application -> Domain
EventiyApp     -> backend HTTP API
```

The `Domain` project is the innermost layer. It has no ASP.NET, EF Core, MediatR, or Angular dependency.

## Solution structure

```text
Eventiy.sln
├── Domain/                 # Aggregates, entities, value objects, errors, Result, domain events
├── Application/            # CQRS commands/queries, handlers, validators, abstractions, behaviors
├── Infrastructure/         # EF Core, repositories, auth, caching, storage, outbox, seed data
├── Eventy.WebApi/          # ASP.NET Core API, controllers, middleware, OpenAPI/Scalar
├── EventiyApp/             # Angular frontend
├── ARCHITECTURE.md         # Full architecture reference
├── ABOUT.md                # Short project overview
└── README.md               # Project introduction and setup guide
```

### Backend projects

| Project | Responsibility |
| --- | --- |
| `Domain` | Enterprise business rules: `Event`, `TicketType`, `Booking`, `User`, value objects, typed errors, domain events, and `Result` types. |
| `Application` | Use cases: MediatR commands/queries, handlers, FluentValidation validators, pipeline behaviors, repository/security/outbox abstractions. |
| `Infrastructure` | External details: EF Core `ApplicationDbContext`, migrations, repositories, JWT, password hashing, Redis caching, local file storage, outbox processing, seed data. |
| `Eventy.WebApi` | Presentation layer: controllers, middleware, CORS, JSON options, OpenAPI, Scalar API reference, static file serving for uploaded event images. |

### Frontend project

| Folder | Responsibility |
| --- | --- |
| `EventiyApp/src/app/core` | Frontend domain models, enums, mappers, and route guards. |
| `EventiyApp/src/app/application/http` | Pure HTTP services that call the backend and return `Result<T>`. |
| `EventiyApp/src/app/application/services` | Use-case orchestration, mapping, caching, auth session state, and booking/event workflows. |
| `EventiyApp/src/app/infrastructure` | Interceptors, directives, toast service, and session storage. |
| `EventiyApp/src/app/features` | Page-level feature components such as home, auth, events, and dashboards. |
| `EventiyApp/src/app/shared` | Reusable UI components and pipes. |

## Backend design

### Domain-Driven Design

The domain model centers on these aggregates:

- `Event`: event lifecycle, type/category, date, location, capacity, ticket types, photos, publication/cancellation/completion state.
- `TicketType`: ticket name, price, capacity, reserved count, sold count, and availability rules.
- `Booking`: user reservation, quantity, total amount, payment method, hold expiry, confirmation/cancellation/refund/expiry state.
- `User`: identity, role, password hash, and organizer approval state.

Important value objects include:

- `EventId`, `TicketTypeId`, `BookingId`, `EventPhotoId`, `UserId`
- `EventName`
- `Address` with optional latitude and longitude
- `Money`
- `Email`
- `Role`
- `PhoneNumber`

Domain methods return `Result` or `Result<T>` for expected business failures. Domain events are raised directly from aggregates with `new XxxEvent(...)` calls and carry `EventMetadata` for correlation, causation, and user context.

### CQRS with MediatR

Application use cases are split into commands and queries:

- Commands implement `ICommand` or `ICommand<TResponse>`.
- Queries implement `IQuery<TResponse>`.
- Handlers return `Result` or `Result<TResponse>`.
- `LoggingPipelineBehavior` records execution details.
- `ValidationPipelineBehavior` runs FluentValidation validators before handlers.

Examples:

- `CreateEventCommand`
- `UpdateEventCommand`
- `AddTicketTypeCommand`
- `CreateBookingCommand`
- `ConfirmBookingCommand`
- `CancelBookingCommand`
- `RegisterUserCommand`
- `LoginCommand`
- `GetEventsQuery`
- `GetEventDetailsQuery`
- `GetBookingsByUserQuery`

### Persistence and outbox

Infrastructure uses EF Core with SQL Server:

- `ApplicationDbContext` for writes.
- `ReadDbContextAdapter` for no-tracking reads.
- Repository implementations for events, bookings, users, event photos, outbox messages, and idempotency state.
- Fluent API configurations in `Infrastructure/Persistence/Configuration`.
- Migrations in `Infrastructure/Migrations`.

`UnitOfWork.CommitAsync()` extracts domain events from tracked aggregate roots, stores them as outbox messages, and commits the transaction. `OutboxProcessor` processes messages in the background, dispatches application domain-event handlers, retries failures, and moves exhausted messages to dead letters.

### Caching

`ICacheService` abstracts cache operations. `RedisCacheService` implements it with StackExchange.Redis and degrades gracefully if Redis is unavailable.

Current cache usage includes:

- Event list cache-aside queries.
- Event details cache-aside queries.
- Cache invalidation after event mutations and booking creation.

### File storage

`LocalFileStorageService` stores uploaded event photos under:

```text
Eventy.WebApi/wwwroot/uploads/events
```

Allowed image types are JPEG, PNG, and WebP. The maximum file size is 5 MB per image.

## Frontend design

The Angular app uses standalone components and layered frontend Clean Architecture.

### Core layer

`core` contains frontend domain types and pure mapping logic:

- Event, booking, auth, and result models.
- Event status, booking status, and user role enums.
- Backend DTO to frontend model mappers.
- Auth and role route guards.

### Application layer

The application layer separates HTTP access from orchestration:

- `AuthHttpService`, `EventHttpService`, `BookingHttpService`, and `EventPhotoHttpService` perform raw API calls.
- `AuthApplicationService`, `EventApplicationService`, and `BookingApplicationService` map responses, manage cache/session state, and expose use-case methods to components.

### Infrastructure layer

Cross-cutting frontend infrastructure includes:

- `auth.interceptor` to attach JWT bearer tokens.
- `error.interceptor` to map HTTP errors to toasts and auth redirects.
- `ToastService` with Angular signals.
- `SessionStorageService` for session-scoped auth state.
- `ImgFallbackDirective` for image fallback behavior.

### Presentation and features

The frontend includes routes for:

- Home page
- Login
- Registration
- Event listing
- Event details
- Event creation
- Organizer dashboard
- Attendee bookings dashboard
- Unauthorized and not-found pages

The Angular development server runs on port `57354` and proxies API requests through `proxy.conf.json`.

## API overview

Controller base paths use ASP.NET Core `[Route("api/[controller]")]`, so current routes are lowercase in the Angular app but resolve case-insensitively on ASP.NET Core by default.

### Events — `/api/event`

| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| `GET` | `/api/event/{id}` | Public | Get event details. |
| `GET` | `/api/event?page=1&pageSize=20&type=Sports&userLatitude=...&userLongitude=...&distanceInKm=20` | Public | Get paginated events with optional type and nearby filters. |
| `POST` | `/api/event` | Organizer/Admin | Create an event. |
| `PUT` | `/api/event/{eventId}/ticket-types` | Organizer/Admin | Add a ticket type. |
| `PUT` | `/api/event/{id}` | Organizer/Admin | Update event details. |
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
| `POST` | `/api/booking` | Any authenticated user | Create a booking. |
| `POST` | `/api/booking/{bookingId}/confirm` | Any authenticated user | Confirm a booking. |
| `PUT` | `/api/booking/{id}/cancel` | Any authenticated user | Cancel a booking. |
| `GET` | `/api/booking/my` | Any authenticated user | Get bookings for the current user. |

### Authentication — `/api/auth`

| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| `POST` | `/api/auth/register` | Public | Register a user. |
| `POST` | `/api/auth/login` | Public | Log in and receive an auth response. |
| `POST` | `/api/auth/organizers/{userId}/approve` | Admin | Approve an organizer account. |

### Admin outbox — `/api/admin/outbox`

| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| `GET` | `/api/admin/outbox/dead-letters` | Admin | List failed outbox messages. |
| `POST` | `/api/admin/outbox/dead-letters/{id}/requeue` | Admin | Requeue a dead-letter message. |

## Tech stack

### Backend

- .NET 9
- ASP.NET Core Web API
- EF Core 9
- SQL Server
- MediatR
- FluentValidation
- JWT Bearer authentication
- BCrypt.Net-Next password hashing
- StackExchange.Redis
- Scalar API reference

### Frontend

- Angular 19
- Standalone components
- Angular signals
- RxJS
- Tailwind CSS
- Karma/Jasmine test setup

## Getting started

### Prerequisites

- .NET 9 SDK
- Node.js and npm
- SQL Server or a SQL Server-compatible database
- Optional: Redis on `localhost:6379` for cache support
- Optional: EF Core CLI for migrations

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

If Redis is unavailable, the cache service logs the failure and the app continues without cache reads/writes.

### JWT settings

`Infrastructure.DependencyInjection` requires `Jwt:Secret` to exist and be at least 32 characters.

For local development, use .NET user secrets:

```bash
dotnet user-secrets set "Jwt:Secret" "replace-with-a-local-development-secret-at-least-32-characters" --project Eventy.WebApi
dotnet user-secrets set "Jwt:Issuer" "Eventiy.Api" --project Eventy.WebApi
dotnet user-secrets set "Jwt:Audience" "Eventiy.Client" --project Eventy.WebApi
dotnet user-secrets set "Jwt:ExpiryMinutes" "1440" --project Eventy.WebApi
```

Do not commit real secrets.

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

`DatabaseSeederService` runs from the API host and seeds initial data when the database has no users/events.

Seed data includes:

- Admin user
- Approved organizer
- Pending organizer
- Multiple attendee users
- FIFA-style sports events with ticket types and bookings

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
    "street": "Stadium Road"
  },
  "description": "Football championship final",
  "type": "Sports",
  "latitude": 30.0444,
  "longitude": 31.2357
}
```

### Add ticket type

```http
PUT /api/event/{eventId}/ticket-types
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "VIP",
  "amount": 250,
  "currency": "USD",
  "capacity": 100
}
```

### Create booking

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

## Project conventions

- Domain remains framework-free: no EF Core, ASP.NET Core, MediatR, or infrastructure dependencies.
- Business validation uses `Result` and typed `Error` values instead of exceptions.
- Application handlers orchestrate use cases and call repositories through interfaces.
- Domain events are raised directly from aggregates and persisted through the outbox.
- Commands and queries are mediated through MediatR.
- FluentValidation validators live in the Application layer.
- Angular HTTP services stay pure; application services map and orchestrate.
- Angular components should use standalone APIs, signals where appropriate, and route guards for protected pages.

## Documentation

- [`ARCHITECTURE.md`](ARCHITECTURE.md): full architecture reference and data-flow walkthroughs.
- [`ABOUT.md`](ABOUT.md): short product summary and project vision.
- [`EventiyApp/README.md`](EventiyApp/README.md): Angular CLI generated frontend notes.
