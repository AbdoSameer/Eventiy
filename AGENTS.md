# Session Summary — Ongoing Project Context

## Goal
Complete all code quality improvements and bug fixes for the Eventiy platform (Clean Architecture .NET + Angular).

## Constraints & Preferences
- Everything must compile with 0 errors on both `dotnet build Eventy.WebApi` and `ng build`
- No indirection layers without value — prefer direct `new XxxEvent(…)` over static factory wrappers
- Domain classes must remain clean: no framework dependencies, pure DDD

## Build Verification
- **dotnet build Eventy.WebApi**: ✅ 0 errors, 31 pre-existing warnings
- **ng build**: ✅ 0 errors, bundle generation complete
- **EF migrations**: `AddProcessedEventsAndDeadLetters` created (needs `dotnet ef database update`)

---

## Completed Work

### Nearby Events + Event Type Classification (Just Completed)
- **`EventType` enum**: Created `Domain/Aggregates/EventAggregate/Enums/EventType.cs` — Music, Tech, Sports, Art, Food, Education, Theater, Outdoors
- **Lat/Lng on Address**: Added `double? Latitude` / `double? Longitude` to `Address` value object with validation
- **Event domain**: Added `EventType Type` property, updated `Create()` and private constructor
- **EF config**: Added `Type` column (int mapping), `Latitude`/`Longitude` columns in `EventConfiguration.cs`
- **CreateEventCommand**: Accepts `EventType Type`, `Latitude`, `Longitude` — passes to `Address.Create` + `Event.Create`
- **GetEventsQuery**: Accepts optional `EventType? Type`, `double? UserLatitude`, `double? UserLongitude`, `double DistanceInKm=20`
- **GetEventsHandler**: Filters by type, filters by bounding box (≈20km) when lat/lng provided; cache key includes all filter params
- **EventController**: `GET /api/event` accepts `type`, `userLatitude`, `userLongitude`, `distanceInKm` query params
- **Migration**: `AddEventTypeAndCoordinates` created
- **DatabaseSeeder**: All 8 seed events tagged `EventType.Sports`
- **Frontend model**: `EventCardDto`, `EventDetailsDto`, `CreateEventCommand` extended with `type`, `latitude`, `longitude`
- **HTTP service**: `getEvents` passes `type`, `userLatitude`, `userLongitude`, `distanceInKm` as query params
- **EventApplicationService**: Added `userLocation` signal, `requestUserLocation()` (HTML5 Geolocation), `toggleNearby()`, passes location to API
- **EventListComponent**: Added "Show Nearby" toggle button in sidebar; category filter calls backend with `type` param
- **EventCreateComponent**: Added category dropdown (maps to EventType), selected as default "Music"

### Redis Caching Layer
- Created `ICacheService` interface with Get/Set/Remove/RemoveByPattern/ClearAll
- `RedisCacheService` implementation using `StackExchange.Redis` — `ConnectionMultiplexer` as Singleton, graceful fallback on Redis failures
- **Event List cache-aside**: `GetEventsHandler` checks `cache:events:list:{page}:{size}` (TTL 30s), misses hit DB and populates cache
- **Event Details cache-aside**: `GetEventDetailsHandler` checks `cache:event:details:{eventId}` (TTL 60s)
- **Cache invalidation**:
  - `CreateEventCommandHandler` → `RemoveByPatternAsync("events:*")` after commit
  - `UpdateEventCommandHandler` → removes `event:details:{id}` + `events:list:*`
  - `CreateBookingCommandHandler` → removes `event:details:{eventId}` (available seats changed)
- Added `StackExchange.Redis 2.8.31` NuGet to Infrastructure
- Registered `RedisCacheService` as Singleton in DI, reads `ConnectionStrings:Redis` from config

### Dead-Letter Table (Just Completed)
- Created `OutboxDeadLetter` entity + EF config (table: `OutboxDeadLetters`)
- Added `MoveToDeadLetterAsync`, `GetDeadLettersAsync`, `RequeueDeadLetterAsync` to `IOutboxRepository` + `OutboxRepository`
- OutboxProcessor moves messages to dead-letter after 3 failed retries (or non-retryable error)
- `AdminController` (`[Authorize(Roles = "Admin")]`):
  - `GET /api/admin/outbox/dead-letters` — list all dead letters
  - `POST /api/admin/outbox/dead-letters/{id}/requeue` — move back to outbox (RetryCount = 0)

### Domain TimeProvider → DateTime Refactoring
- **Removed `TimeProvider` entirely from all domain methods** across 5 domain files (Event.cs, Booking.cs, TicketType.cs, EventPhoto.cs, User.cs)
- Domain methods now receive a **resolved `DateTime utcNow`** instead of a clock — the Application layer calls `_timeProvider.GetUtcNow().UtcDateTime` and passes the value in
- **Event.cs**: 23 methods changed (Create, Publish, Cancel, Complete, Reopen, all ticket management, all seat management, all photo management, all property updates)
- **Booking.cs**: 8 methods + private constructor changed
- **TicketType.cs**: 8 methods changed (ReleaseSeats, ConfirmReservation, SellDirect, RefundSeats, Create, UpdatePrice, UpdateCapacity, UpdateName)
- **EventPhoto.cs**: `Create()` changed
- **User.cs**: `Create()` changed
- **14 callers updated** (13 Application handlers + DatabaseSeeder)

### Idempotency Pipeline
- **5-step idempotency architecture** fully implemented:
  1. **Event Unique ID**: `DomainEvent.Id` (Guid) already exists — used as the deduplication key
  2. **ProcessedEvents table**: Created `Infrastructure/Persistence/ProcessedEvent.cs` entity + `ProcessedEventConfiguration.cs` (PK: `EventId`, unique index on `IdempotencyKey`)
  3. **Check before execution**: `BookingCreatedEventHandler` and `BookingCancelledEventHandler` both check `_idempotencyStore.IsProcessedAsync(@event.Id)` before executing
  4. **External integrations**: Architecture supports passing `EventId` as idempotency key to payment gateways (not yet wired — no external calls exist)
  5. **Registration on success**: Both handlers call `_idempotencyStore.MarkAsProcessedAsync()` after successful side effects
- **IIdempotencyStore interface**: Changed from `string idempotencyKey` to `Guid eventId`
- **IdempotencyStore implementation**: `Infrastructure/Persistence/Repositories/IdempotencyStore.cs` — uses `ProcessedEvents` DbSet
- **Fixed dead handler bug**: `IDomainEventHandler<T>` was never wired to the OutboxProcessor (handlers were dead code). Modified `OutboxProcessor.ProcessSingleMessageAsync` to resolve `IDomainEventHandler<T>` via `IServiceProvider` and call `HandleAsync` directly, eliminating the broken MediatR `IPublisher` dispatch
- **DomainEventHandlerException**: Created `Domain/Common/DomainEventHandlerException.cs` for handler failure propagation
- **BookingCancelledEventHandler**: Added `IIdempotencyStore` injection + idempotency check before releasing seat capacity

### Memory Leak Fixes (Angular)
- **EventDetailComponent** (3 subscriptions): Added `DestroyRef` + `takeUntilDestroyed`
- **EventListComponent** (1 subscription): Same fix
- **LoginComponent** (1 subscription): Same fix

### BUG #19 — XSS (localStorage)
- Moved JWT from `localStorage` to `sessionStorage` in `auth-application.service.ts`
- Token clears on tab close, limiting XSS exposure window
- Documented HttpOnly cookies as the ideal long-term fix

### BUG #20 — eventsCache$ invalidation
- Added 30-second TTL (`CACHE_TTL_MS`) to `eventsCache$`
- `invalidateCache()` called on all 7 mutation methods

### Previous Work (Summary)
- **DomainEventFactory deletion**: Deleted static factory, inlined 26 direct `new XxxEvent(…)` calls, fixed 3 pre-existing parameter bugs
- **User.cs Id shadowing**: Removed `public UserId Id` that shadowed base property
- **Leaky Role in domain**: Removed `Role` from `Booking.Confirm()/Cancel()`, moved authorization to handlers
- **ValidationPipelineBehavior reflection**: Replaced fragile reflection with type-safe `ResultHelper.Failure<T>()`
- **OutboxMessageService concrete dependency**: Changed `OutboxRepository` to `IOutboxRepository`
- **IDateTimeProvider → TimeProvider**: Replaced custom interface with .NET 9 built-in `TimeProvider` across ~74 files
- **CORS**: Added `.AllowCredentials()`, split dev/prod origins from config
- **BUG #15**: `DeleteEvent` [HttpDelete] → [HttpPut("{id}/cancel")]
- **BUG #16**: Created `environment.prod.ts` + `fileReplacements` in angular.json
- **Full-Stack Architectural Audit**: 4-section report
- **Media HttpClientBase**: Extracted abstract base, fixed CancelBooking POST→PUT, fixed fallback key bug
- **SMELL fixes**: Template cleanup, ImgFallbackDirective
- **MED-01**: Correlation/Causation chain
- **Frontend Clean Architecture**: 4-layer layout

## Key Decisions
- `RedisCacheService` is Singleton — `ConnectionMultiplexer` is designed to be shared; graceful degradation (log+skip) on Redis failures
- Cache keys prefixed with `cache:` for namespace isolation in Redis
- `ProcessedEvent` uses `EventId` (Guid) as PK — aligns with `DomainEvent.Id`
- OutboxProcessor dispatches to `IDomainEventHandler<T>` via `IServiceProvider.GetService(MakeGenericType)` instead of MediatR `IPublisher` — avoids coupling Domain to MediatR's `INotification`
- `DomainEventHandlerException` propagates handler failures up to OutboxProcessor for retry/failure tracking
- Handlers register `ProcessedAt` in the same DbContext — shares the same transaction scope
- Seat lifecycle: `ReserveSeats` (Reserved++) → `ConfirmReservation` (Reserved--, Sold++) → `ReleaseSeats` (Reserved--) / `RefundSeats` (Sold--)

## Relevant Files
- **Redis caching — new**: `Application/Abstractions/Caching/ICacheService.cs`, `Infrastructure/Caching/RedisCacheService.cs`
- **Dead-letter — new**: `Infrastructure/Persistence/Outbox/OutboxDeadLetter.cs`, `Infrastructure/Persistence/Configuration/OutboxDeadLetterConfiguration.cs`, `Eventy.WebApi/Controllers/AdminController.cs`
- **Redis — updated handlers**: `GetEventsHandler.cs`, `GetEventDetailsHandler.cs`, `CreateEventCommandHandler.cs`, `UpdateEventCommandHandler.cs`, `CreateBookingCommandHandler.cs`, `Infrastructure/DependencyInjection.cs`, `Infrastructure/Infrastructure.csproj`
- **Dead-letter — updated**: `Application/Abstractions/Outbox/IOutboxRepository.cs`, `Infrastructure/Persistence/Repositories/OutboxRepository.cs`, `Infrastructure/BackgroundJobs/OutboxProcessor.cs`, `Infrastructure/Persistence/ApplicationDbContext.cs`
- **Domain refactoring**: `Event.cs`, `Booking.cs`, `TicketType.cs`, `EventPhoto.cs`, `User.cs`
- **Idempotency — new**: `Infrastructure/Persistence/ProcessedEvent.cs`, `Infrastructure/Persistence/Configuration/ProcessedEventConfiguration.cs`, `Infrastructure/Persistence/Repositories/IdempotencyStore.cs`, `Domain/Common/DomainEventHandlerException.cs`
- **Idempotency — updated**: `Application/Abstractions/Persistence/IIdempotencyStore.cs`, `Application/EventHandlers/BookingCreatedEventHandler.cs`, `Application/EventHandlers/BookingCancelledEventHandler.cs`, `Infrastructure/BackgroundJobs/OutboxProcessor.cs`, `Infrastructure/Persistence/ApplicationDbContext.cs`, `Infrastructure/DependencyInjection.cs`
- **Angular fixes**: `event-detail.component.ts`, `event-list.component.ts`, `login.component.ts`, `auth-application.service.ts`, `event-application.service.ts`

## Critical Context
- Backend port: `https://localhost:7001`; Angular dev port: `:57354`
- Domain has zero framework dependencies — no MediatR, no EF, no ASP.NET
- DB has 2 existing migrations + 1 pending (`AddProcessedEventsAndDeadLetters`)
- OutboxProcessor polls every 5s, batch size 50, with UPDLOCK/READPAST and 5min lock timeout
- Two event handlers exist: `BookingCreatedEventHandler` (validation + idempotency) and `BookingCancelledEventHandler` (capacity release + idempotency)
- `IEventValidator<T>` for `BookingCreatedEvent` exists but no others yet
- Redis connection reads from `ConnectionStrings:Redis` (default `localhost:6379`)
