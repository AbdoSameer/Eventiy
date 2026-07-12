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
- **Event Photos cache-aside**: key `cache:event:photos:{eventId}`, TTL 120s (nearly-static data)
- **Cache invalidation**:
  - CreateEvent → `RemoveByPatternAsync("events:*")`
  - UpdateEvent → evict `event:details:{id}` + `events:list:*`
  - CreateBooking → evict `event:details:{id}` (refresh seats capacity, prevent overbooking)
  - UploadEventPhotos / DeleteEventPhoto / ReorderEventPhotos / UpdatePhotoMetadata → evict `event:photos:{eventId}`
  - SetCoverPhoto → evict BOTH `event:photos:{eventId}` (IsCover flag) + `event:details:{eventId}` (computed CoverPhotoUrl)
- **Decisions**: All invalidation runs *after* successful `CommitAsync` (no eviction before tx success). Booking queries intentionally uncached — financial/personal data with high mutation rate; stale data risk outweighs cache benefit.
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

### Seating Chart Reactive Gap Fix
- **Problem**: `checkoutSelection()` called `lockOrchestrator.acquire()` per seat, which short-circuited (`return true`) if the seat was already selected in store — meaning **no LOCK was ever sent** over WebSocket
- **`executeFinalBooking()`**: new async method that directly dispatches LOCK messages via `liveSync.send()` for all selected seats, bypassing the `acquire()` short-circuit
- **Submitting signal**: `submitting` signal guards `handleSeatClick` (prevents map clicks during in-flight), and drives `.is-loading` CSS class on the floating pill
- **Floating pill restyled**: moved outside `.sc-canvas` to avoid `overflow: hidden` clipping; uses flex-flow natural positioning with `align-self: center`; shows seat count, total price, and a CSS-only spinner during submission
- **`SeatLockOrchestratorService.acquire()`** kept for per-click optimistic flow; `executeFinalBooking()` is the checkout-level dispatch

### Booking API Integration
- **Problem**: `executeFinalBooking()` only sent WebSocket LOCK messages (no backend WS handler existed) and never called `POST /api/booking` — no booking was ever created in the database
- **Fix**: `executeFinalBooking()` now:
  1. Fetches real event data via `EventApplicationService` to get `ticketTypes`
  2. Uses the first ticket type's ID (auto-selected, stored in `selectedTicketTypeId` signal)
  3. Sends LOCK messages (best-effort over WebSocket)
  4. Calls `POST /api/booking` via `BookingApplicationService.createBooking()` with `{ eventId, ticketTypeId, quantity: selectedSeats.length, paymentMethod }`
  5. Navigates to `/bookings/{bookingId}` on success showing booking detail page
- Also changed `liveSync.connect(eventId, { simulate: true })` since the backend WebSocket middleware doesn't exist yet

### Booking Total Mismatch Fix
- **Problem**: Seating chart displayed `sum(SeatNode.price)` from mock D3 data, while backend charged `ticketType.Price * quantity`. If mock seat prices differed from the real ticket type price, the two totals didn't match.
- **Fix**: Added `bookingTotal` computed signal that derives from `eventData.ticketTypes[ticketTypeId].price * store.totalSelected()` — the same formula the backend uses. Floating pill and sidebar now render `bookingTotal()` instead of `store.totalCartPrice()` / `store.totalPrice()`.

### Booking Lifecycle Fix
- Inventory reservation with atomic `ReserveSeats()`, deferred persistence via `ReferenceCode`/`HoldExpiresAt`/`PaymentMethod`, mock `StripePaymentGateway` returning `null` URL for dev, admin booking endpoints, attendee self-cancel for Pending only
- **`BookingDetailComponent`**: created `features/bookings/booking-detail/` with route `bookings/:id`; shows all fields + Cancel/View Event; removed "Confirm Payment" button (security — user must not self-confirm deferred payment)

### Real-time WebSocket Infrastructure
- **Backend**: `WebSocketConnectionManager`, `RedisPubSubBroadcaster` (channel `seats:event:{eventId}`), `WebSocketMiddleware` at `/ws/venues/{eventId}` with JWT auth via query param, 15s PING/PONG heartbeat, LOCK handler calls `Event.ReserveSeats()` atomically, broadcasts DELTA or sends COLLISION to loser
- **Frontend**: `LiveStateSyncService` connects to real backend WS, handles DELTA/COLLISION/PING/ACK/CONNECTED; `SeatLockOrchestratorService` sends LOCK/UNLOCK over WS instead of mocked `setTimeout`

### Organizer Dashboard Fix
- Removed `.slice(0, 5)` limit on `loadBookingsForEvents()` to show all bookings; changed `statusFilter` from plain string to `signal('')` so computed `filteredBookings` reactively filters

### Seating Chart TypeScript Build Errors
- Added missing `onBlockClick` handler to `renderVenue()` call; added `BlockGroup` import and `handleBlockClick()` method; fixed `handleHover` parameter types for `BlockGroup`; cleaned unused imports in `seat-lock-orchestrator.service.ts` and `live-state-sync.service.ts`

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
- `executeFinalBooking()` bypasses `acquire()` short-circuit by sending LOCK directly via `liveSync.send()` — `acquire()` is kept for per-click optimistic flow; `executeFinalBooking()` is the checkout-level dispatch
- Floating pill uses flex-flow natural positioning (`align-self: center`) instead of `position: absolute` to avoid `overflow: hidden` clipping; shows CSS-only spinner during submission
- `executeFinalBooking()` now integrates with the REST API: after sending LOCKs (best-effort), it calls `POST /api/booking` and navigates to `/bookings/{bookingId}` on success

## Next Steps
1. Force push rewritten git history to remote (`git push --force --all`)
2. Run `dotnet ef database update` against dev database to apply all 3 pending migrations
3. Add `ConnectionStrings:Redis` to `appsettings.Development.json` or User Secrets for local Redis
4. Add `Jwt:Secret` to staging/production environment variables or Azure Key Vault
5. Inform collaborators to `git rebase` their feature branches after force-push

## Critical Context
- Backend port: `https://localhost:7001`; Angular dev port: `:57354`
- Project targets `net9.0` — `TimeProvider` is built-in, no extra packages needed
- DB has 5 migrations total: 2 original + 3 pending (`AddProcessedEventsAndDeadLetters`, `AddEventTypeAndCoordinates`)
- OutboxProcessor polls every 5s, batch size 50, with UPDLOCK/READPAST and 5min lock timeout
- Two event handlers (both with idempotency): `BookingCreatedEventHandler` (validation), `BookingCancelledEventHandler` (capacity release)
- `IEventValidator<BookingCreatedEvent>` exists but no others yet
- Redis connection reads from `ConnectionStrings:Redis` (default `localhost:6379`)
- **Caching coverage**: 3/6 read queries cached — Events list (30s), Event details (60s), Event photos (120s). Booking queries (`GetBookingsByUser`, `GetBookingDetails`, `GetBookingByEvent`) intentionally uncached — financial/personal data with high mutation rate.
- All cache invalidation runs *after* successful `UnitOfWork.CommitAsync()` — no eviction before tx success
- JWT secret loaded from User Secrets (dev) / env vars (prod); appsettings.json has `Issuer`, `Audience`, `ExpiryMinutes` only
- Git history rewritten: old secret replaced with `REVOKED_JWT_SECRET` across all 62 commits; backup refs deleted; repo fully gc'd
- **Seat lock flow**: `executeFinalBooking()` snapshots `selectedSeatsMetadata()`, sends LOCK for each via `liveSync.send()`, uses `submitting` signal to lock UI; `acquire()` kept for per-click optimistic flow

## Relevant Files
- **JWT security**: `Eventy.WebApi/appsettings.json` (secret removed), `.gitignore` (local.json pattern), `Infrastructure/DependencyInjection.cs` (startup validation), `Properties/secrets.json` (generated, excluded from git)
- **Nearby Events — new**: `Domain/Aggregates/EventAggregate/Enums/EventType.cs`
- **Nearby Events — updated**: `Address.cs`, `Event.cs`, `EventConfiguration.cs`, `CreateEventCommand.cs` + handler, `GetEventsQuery.cs` + `GetEventsHandler.cs` + `EventCardResponse.cs`, `EventController.cs`, `DatabaseSeeder.cs`
- **Redis — new**: `Application/Abstractions/Caching/ICacheService.cs`, `Infrastructure/Caching/RedisCacheService.cs`
- **Redis — updated**: `GetEventsHandler.cs`, `GetEventDetailsHandler.cs`, `GetEventPhotosQueryHandler.cs`, `CreateEventCommandHandler.cs`, `UpdateEventCommandHandler.cs`, `CreateBookingCommandHandler.cs`, `UploadEventPhotosCommandHandler.cs`, `DeleteEventPhotoCommandHandler.cs`, `ReorderEventPhotosCommandHandler.cs`, `SetCoverPhotoCommandHandler.cs`, `UpdatePhotoMetadataCommandHandler.cs`, `Infrastructure/DependencyInjection.cs`, `Infrastructure/Infrastructure.csproj`
- **Dead-letter — new**: `OutboxDeadLetter.cs`, `OutboxDeadLetterConfiguration.cs`, `AdminController.cs`
- **Dead-letter — updated**: `IOutboxRepository.cs`, `OutboxRepository.cs`, `OutboxProcessor.cs`, `ApplicationDbContext.cs`
- **Idempotency**: `ProcessedEvent.cs`, `IIdempotencyStore.cs`, `IdempotencyStore.cs`, `BookingCreatedEventHandler.cs`, `BookingCancelledEventHandler.cs`, `OutboxProcessor.cs`, `DomainEventHandlerException.cs`
- **Domain refactoring**: `Event.cs`, `Booking.cs`, `TicketType.cs`, `EventPhoto.cs`, `User.cs`
- **Seating chart (updated)**: `seating-chart.component.ts` (executeFinalBooking, submitting signal), `seating-chart.component.html` (pill restyled outside canvas), `seating-chart.component.scss` (spinner, loading state)
- **Seating chart services**: `seat-lock-orchestrator.service.ts`, `live-state-sync.service.ts` (real WS integration), `venue-graph-renderer.service.ts` (BlockClick handler)
- **Frontend (updated)**: `event.model.ts`, `event.mapper.ts`, `event.http-service.ts`, `event-application.service.ts`, `event-list.component.ts`, `event-create.component.ts`
- **Migrations**: `AddProcessedEventsAndDeadLetters.cs`, `AddEventTypeAndCoordinates.cs`
