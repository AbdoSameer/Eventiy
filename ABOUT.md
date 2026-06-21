# About Eventiy

Eventiy is an event and ticket booking backend for many event types, including matches, parties, conferences, shows, and other gatherings.

The goal of the project is to provide a clean, maintainable API where event organizers can define events and ticket types, and customers can book, confirm, or cancel tickets while the system protects business rules such as capacity limits and valid booking states.

## What the system does

- Manages events with name, date, location, description, status, and capacity.
- Supports multiple ticket types per event.
- Tracks bookings for events and ticket types.
- Validates business rules with domain objects and explicit result values.
- Persists event and booking data using Entity Framework Core.
- Exposes use cases through ASP.NET Core controllers.

## Design approach

Eventiy follows:

- **Domain-Driven Design** to keep event, ticket, and booking rules inside the domain model.
- **Clean Architecture** to separate domain logic, use cases, infrastructure, and API delivery.
- **Result pattern** to return clear success or failure outcomes for expected business rules.
- **MediatR** to decouple API controllers from application commands and queries.

## Main domain concepts

- **Event**: A match, party, conference, show, or other bookable occasion.
- **Ticket type**: A priced ticket category with its own capacity.
- **Booking**: A customer reservation for a ticket type at an event.
- **Value objects**: Reusable validated concepts such as money, address, email, and phone number.

## Project vision

Eventiy is intended to grow into a full event booking platform with stronger organizer workflows, customer booking flows, payments, availability checks, and clear domain rules that remain easy to maintain as the product expands.
