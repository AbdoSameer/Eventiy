# Eventiy

Eventiy is a .NET event and ticket booking API for managing many kinds of events, such as matches, parties, conferences, shows, and other public or private gatherings.

The project is built around Domain-Driven Design, Clean Architecture, the Result pattern, and MediatR so the business rules stay close to the domain model while application use cases remain easy to test and extend.

## Features

- Create and list events
- View event details
- Add ticket types to an event
- Make, confirm, cancel, and query bookings
- Model event capacity, ticket type capacity, pricing, addresses, user IDs, booking status, and event lifecycle rules
- Return explicit success or failure results instead of throwing exceptions for expected business validation failures

## Architecture

The solution is split into these main layers:

| Project | Responsibility |
| --- | --- |
| `Domain` | Core business model, aggregates, entities, value objects, domain errors, and `Result` types |
| `Application` | Use cases, commands, queries, MediatR handlers, persistence abstractions, validation, and application services |
| `Infrastructure` | EF Core database context, migrations, repositories, unit of work, and external service implementations |
| `Eventy.WebApi` | ASP.NET Core API host, controllers, dependency injection, OpenAPI, and Scalar API reference |

Dependency flow:

```text
Eventy.WebApi -> Application -> Domain
Eventy.WebApi -> Infrastructure -> Application/Domain
```

The `Domain` layer does not depend on infrastructure or web concerns.

## Patterns and practices

### Domain-Driven Design

The domain model contains the core event booking concepts:

- `Event` aggregate
- `Booking` aggregate
- `TicketType` entity
- Value objects such as `Address`, `Money`, `Email`, and `PhoneNumber`
- Domain errors and lifecycle rules for events, tickets, and bookings

### Clean Architecture

Business logic is separated from delivery and persistence concerns:

- Controllers only receive HTTP requests and send commands or queries through MediatR.
- Application handlers coordinate use cases through repository and unit-of-work abstractions.
- Infrastructure implements persistence with Entity Framework Core.
- Domain objects enforce business invariants.

### Result pattern

Operations that can fail return `Result` or `Result<T>` with either:

- a success value, or
- an error message/value describing why the operation failed.

This keeps expected validation and business-rule failures explicit.

### MediatR

Commands and queries are sent through MediatR:

- Commands mutate state, such as creating an event or making a booking.
- Queries read data, such as fetching event details or booking details.
- Handlers contain the application workflow for each use case.

## API endpoints

Base controller routes currently include:

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/api/Event` | Get all events |
| `GET` | `/api/Event/{id}` | Get event details |
| `POST` | `/api/Event` | Create a new event |
| `POST` | `/api/Event/{eventId}/ticket-types` | Add a ticket type to an event |
| `GET` | `/api/Booking/{id}` | Get booking details |
| `GET` | `/api/Booking/bookings/event/{eventId}` | Get bookings for an event |
| `POST` | `/api/Booking` | Make a booking |
| `POST` | `/api/Booking/booking/{bookingId}/confirm` | Confirm a booking |
| `POST` | `/api/Booking/booking/{bookingId}cancel` | Cancel a booking |

When the API runs in development mode, OpenAPI and Scalar API reference are enabled.

## Tech stack

- .NET 9
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- MediatR
- Scalar API reference

## Getting started

### Prerequisites

- .NET 9 SDK
- SQL Server or SQL Server-compatible database
- EF Core CLI tools, if you want to apply migrations from the command line

### Restore packages

```bash
dotnet restore Eventiy.sln
```

### Configure the database

Update the `DefaultConnection` connection string in:

```text
Eventy.WebApi/appsettings.json
```

Example:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=EventiyDB;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

For local development, you can also use user secrets or environment-specific settings instead of committing machine-specific connection strings.

### Apply migrations

If `dotnet-ef` is not installed:

```bash
dotnet tool install --global dotnet-ef
```

Then apply migrations:

```bash
dotnet ef database update --project Infrastructure --startup-project Eventy.WebApi
```

### Build

```bash
dotnet build Eventiy.sln
```

### Run the API

```bash
dotnet run --project Eventy.WebApi
```

In development, open the Scalar API reference from the URL shown in the terminal, usually under the app host with the Scalar route.

## Example create event request

```http
POST /api/Event
Content-Type: application/json

{
  "name": "Championship Match",
  "capacity": 50000,
  "date": "2026-12-20T18:00:00Z",
  "location": {
    "country": "Egypt",
    "city": "Cairo",
    "street": "Stadium Road"
  },
  "description": "Football championship final"
}
```

## Example add ticket type request

```http
POST /api/Event/{eventId}/ticket-types
Content-Type: application/json

{
  "name": "VIP",
  "amount": 250,
  "currency": "USD",
  "capacity": 100
}
```

## Example make booking request

```http
POST /api/Booking
Content-Type: application/json

{
  "eventId": "00000000-0000-0000-0000-000000000000",
  "ticketTypeId": "00000000-0000-0000-0000-000000000000",
  "quantity": 2
}
```

## Repository status

This repository is in active development. Some APIs, names, and workflows may change as the event booking domain grows.
