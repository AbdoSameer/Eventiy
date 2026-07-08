# Session Summary — Ongoing Project Context

## Goal
Complete code quality improvements, flash-sale performance optimization, and security hardening for the Eventiy platform (Clean Architecture .NET 9 + Angular).

## Constraints & Preferences
- Everything must compile with 0 errors on both `dotnet build Eventy.WebApi` and `ng build`
- No indirection layers without value — prefer direct `new XxxEvent(…)` over static factory wrappers
- Domain classes must remain clean: no framework dependencies, pure DDD
- Domain must not know about Authorization (roles), EF Core, MediatR, or `TimeProvider`
- Use `[Authorize]` on controllers/actions, not in domain logic
- Use built-in .NET abstractions (`TimeProvider`, `ConcurrencyException`, `IDistributedCache`) over custom ones where possible

## Build Verification
- **dotnet build Eventy.WebApi**: ✅ 0 errors, 31 pre-existing warnings
- **ng build**: ✅ 0 errors, bundle generation complete
- **EF migrations (3 pending)**: `AddProcessedEventsAndDeadLetters`, `AddEventTypeAndCoordinates`

---

## Completed Work

### JWT Secret Security (Just Completed)
- **Removed `Jwt:Secret` from appsettings.json** — no secret in source control
- **User Secrets** initialized (`dotnet user-secrets init`), new 64-char cryptographically-random secret set via `dotnet user-secrets set "Jwt:Secret" "..."`
- **Startup validation** in `Infrastructure/DependencyInjection.cs`: throws if `Jwt:Secret` is null/empty or < 32 chars
- **`.gitignore`**: added `appsettings.*.local.json` pattern
- **Git history rewrite**: `git filter-branch` replaced old secret (`This_Is_My_Secret_Key_For_Eventiy_Project_Ticket_Event_Booking`) with `REVOKED_JWT_SECRET` across all 62 commits
- **Cleanup**: backup refs deleted, `git gc --aggressive --prune=now` run; repo 100% packed, no loose objects

### Nearby Events + Event Type Classification
- **EventType enum**: `Domain/Aggregates/EventAggregate/Enums/EventType.cs` — Music, Tech, Sports, Art, Food, Education, Theater, Outdoors
- **Lat/Lng on Address**: `double? Latitude` / `double? Longitude` with validation
- **Event domain**: Added `EventType Type`, updated `Create()` + private constructor
- **EF config**: `EventConfiguration.cs` — Type (int), Latitude/Longitude columns
- **CreateEventCommand**: Accepts `EventType`, `Latitude`, `Longitude`
- **GetEventsQuery**: Filters by `EventType?`, bounding box distance (≈20km via ±0.18°)
- **GetEventsHandler**: Cache key includes all filter params
- **DatabaseSeeder**: 8 seed events tagged `EventType.Sports`
- **Frontend**: models extended, HTTP service passes geolocation params, `EventApplicationService` (userLocation signal, geolocation API, toggleNearby), EventListComponent (Show Nearby toggle), EventCreateComponent (category dropdown with default "Music")

### Redis Caching Layer
- **ICacheService**: Get/Set/Remove/RemoveByPattern/ClearAll
- **RedisCacheService**: `StackExchange.Redis` `ConnectionMultiplexer` as Singleton, graceful fallback
- **Event List cache-aside**: key `cache:events:list:{page}:{size}:{type}:{lat}:{lng}:{dist}`, TTL 30s
- **Event Details cache-aside**: key `cache:event:details:{eventId}`, TTL 60s
- **Cache invalidation**: Create → `RemoveByPatternAsync("events:*")`; Update → evict detail + list; Booking → evict detail
- **NuGet**: `StackExchange.Redis 2.8.31` added to Infrastructure

### Dead-Letter Table
- **OutboxDeadLetter** entity + EF config (table: `OutboxDeadLetters`)
- **IOutboxRepository**: `MoveToDeadLetterAsync`, `GetDeadLettersAsync`, `RequeueDeadLetterAsync`
- **OutboxProcessor**: moves to dead-letter after 3 failed retries or non-retryable error
- **AdminController** (`[Authorize(Roles = "Admin")]`): `GET /api/admin/outbox/dead-letters`, `POST .../requeue`

### Concurrency Retry
- **3-attempt retry loop** in `CreateBookingCommandHandler`
- **ConcurrencyException** class added
- **UnitOfWork.CommitAsync()** wraps `DbUpdateConcurrencyException` and re-throws as `ConcurrencyException`

### Domain TimeProvider → DateTime Refactoring
- **Removed `TimeProvider`** entirely from 5 domain files: `Event.cs` (23 methods), `Booking.cs` (8), `TicketType.cs` (8), `EventPhoto.cs` (1), `User.cs` (1)
- Domain methods receive `DateTime utcNow` — Application layer resolves from `_timeProvider.GetUtcNow().UtcDateTime`
- **14 callers updated** (13 Application handlers + DatabaseSeeder)

### Idempotency Pipeline
- **ProcessedEvents table** (PK: `EventId`, unique index on `IdempotencyKey`)
- **IIdempotencyStore**: `IsProcessedAsync(Guid eventId)`, `MarkAsProcessedAsync` — changed from `string idempotencyKey`
- **BookingCreatedEventHandler** + **BookingCancelledEventHandler**: both check `IsProcessedAsync(@event.Id)` then `MarkAsProcessedAsync()`
- **Fixed dead handler bug**: OutboxProcessor now resolves `IDomainEventHandler<T>` via `IServiceProvider.MakeGenericType` instead of broken MediatR `IPublisher` dispatch
- **DomainEventHandlerException**: new exception class for handler failure propagation

### Previous Work
- **DomainEventFactory deletion**: Deleted static factory, inlined 26 direct `new XxxEvent(…)` calls, fixed 3 pre-existing parameter bugs
- **User.cs Id shadowing**: Removed `public UserId Id` that shadowed base property
- **Leaky Role in domain**: Removed `Role` from `Booking.Confirm()/Cancel()`, moved authorization to handlers
- **ValidationPipelineBehavior reflection**: Replaced fragile reflection with type-safe `ResultHelper.Failure<T>()`
- **OutboxMessageService concrete dependency**: Changed `OutboxRepository` to `IOutboxRepository`
- **IDateTimeProvider → TimeProvider**: Replaced custom interface with .NET 9 built-in `TimeProvider` across ~74 files
- **CORS**: Added `.AllowCredentials()`, split dev/prod origins from config
- **BUG #15**: `DeleteEvent` [HttpDelete] → [HttpPut("{id}/cancel")]
- **BUG #16**: Created `environment.prod.ts` + `fileReplacements` in angular.json
- **BUG #19**: Moved JWT from `localStorage` to `sessionStorage`
- **BUG #20**: Added 30s TTL to `eventsCache$` + `invalidateCache()` on all 7 mutation methods
- **Memory leaks (Angular)**: `DestroyRef` + `takeUntilDestroyed` on 3 components
- **Full-Stack Architectural Audit**: 4-section report
- **Media HttpClientBase**: Extracted abstract base, fixed CancelBooking POST→PUT, fixed fallback key bug
- **SMELL fixes**: Template cleanup, ImgFallbackDirective
- **MED-01**: Correlation/Causation chain
- **Frontend Clean Architecture**: 4-layer layout

## Key Decisions
- `RedisCacheService` is Singleton — `ConnectionMultiplexer` designed to be shared; graceful degradation (log+skip) on Redis failures
- Cache keys prefixed with `cache:` for Redis namespace isolation; filter params included in key to avoid stale cross-filter results
- Cache invalidation: `RemoveByPatternAsync("events:*")` on create, targeted removal on update/booking
- `MoveToDeadLetterAsync` copies all fields then deletes original — clean separation, no soft-delete flags
- `AdminController` injects `IOutboxRepository` directly (infrastructure management tool)
- Bounding box (±0.18° ≈ 20km) is coarse SQL-level filter — sufficient for initial nearby query accuracy
- `EventType` enum values align with frontend `EVENT_CATEGORIES` array
- `Jwt:Secret` loaded via .NET configuration hierarchy: User Secrets (dev) → Environment Variables/Azure Key Vault (prod)
- `git filter-branch` replaced old secret with `REVOKED_JWT_SECRET` across all 62 commits — collaborators must `git rebase` on force-push
- Domain has zero framework dependencies — no MediatR, no EF, no ASP.NET, no `TimeProvider`
- Seat lifecycle: `ReserveSeats` (Reserved++) → `ConfirmReservation` (Reserved--, Sold++) → `ReleaseSeats` (Reserved--) / `RefundSeats` (Sold--)

## Next Steps
1. Force push rewritten git history to remote (`git push --force --all`)
2. Run `dotnet ef database update` against dev database to apply all 3 pending migrations
3. Add `ConnectionStrings:Redis` to `appsettings.Development.json` or User Secrets for local Redis
4. Add `Jwt:Secret` to staging/production environment variables or Azure Key Vault
5. Verify Angular build (`ng build`) still produces no errors
6. Inform collaborators to `git rebase` their feature branches after force-push

## Critical Context
- Backend port: `https://localhost:7001`; Angular dev port: `:57354`
- Project targets `net9.0` — `TimeProvider` is built-in, no extra packages needed
- DB has 5 migrations total: 2 original + 3 pending (`AddProcessedEventsAndDeadLetters`, `AddEventTypeAndCoordinates`)
- OutboxProcessor polls every 5s, batch size 50, with UPDLOCK/READPAST and 5min lock timeout
- Two event handlers (both with idempotency): `BookingCreatedEventHandler` (validation), `BookingCancelledEventHandler` (capacity release)
- `IEventValidator<BookingCreatedEvent>` exists but no others yet
- Redis connection reads from `ConnectionStrings:Redis` (default `localhost:6379`)
- JWT secret loaded from User Secrets (dev) / env vars (prod); appsettings.json has `Issuer`, `Audience`, `ExpiryMinutes` only
- Git history rewritten: old secret replaced with `REVOKED_JWT_SECRET` across all 62 commits; backup refs deleted; repo fully gc'd

## Relevant Files
- **JWT security**: `Eventy.WebApi/appsettings.json` (secret removed), `.gitignore` (local.json pattern), `Infrastructure/DependencyInjection.cs` (startup validation), `Properties/secrets.json` (generated, excluded from git)
- **Nearby Events — new**: `Domain/Aggregates/EventAggregate/Enums/EventType.cs`
- **Nearby Events — updated**: `Address.cs`, `Event.cs`, `EventConfiguration.cs`, `CreateEventCommand.cs` + handler, `GetEventsQuery.cs` + `GetEventsHandler.cs` + `EventCardResponse.cs`, `EventController.cs`, `DatabaseSeeder.cs`
- **Redis — new**: `Application/Abstractions/Caching/ICacheService.cs`, `Infrastructure/Caching/RedisCacheService.cs`
- **Redis — updated**: `GetEventsHandler.cs`, `GetEventDetailsHandler.cs`, `CreateEventCommandHandler.cs`, `UpdateEventCommandHandler.cs`, `CreateBookingCommandHandler.cs`, `Infrastructure/DependencyInjection.cs`, `Infrastructure/Infrastructure.csproj`
- **Dead-letter — new**: `OutboxDeadLetter.cs`, `OutboxDeadLetterConfiguration.cs`, `AdminController.cs`
- **Dead-letter — updated**: `IOutboxRepository.cs`, `OutboxRepository.cs`, `OutboxProcessor.cs`, `ApplicationDbContext.cs`
- **Idempotency**: `ProcessedEvent.cs`, `IIdempotencyStore.cs`, `IdempotencyStore.cs`, `BookingCreatedEventHandler.cs`, `BookingCancelledEventHandler.cs`, `OutboxProcessor.cs`, `DomainEventHandlerException.cs`
- **Domain refactoring**: `Event.cs`, `Booking.cs`, `TicketType.cs`, `EventPhoto.cs`, `User.cs`
- **Frontend (updated)**: `event.model.ts`, `event.mapper.ts`, `event.http-service.ts`, `event-application.service.ts`, `event-list.component.ts`, `event-create.component.ts`
- **Migrations**: `AddProcessedEventsAndDeadLetters.cs`, `AddEventTypeAndCoordinates.cs`
