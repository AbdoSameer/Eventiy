## Refactoring Plan: Event Seating Chart → Reusable Embeddable Component

### Overview
Transform the monolithic standalone `/seating` page into a clean, service-driven, embeddable `<app-event-seating-chart>` component that lives inside the event-details page. Delete the old standalone route and all its files.

---

### Phase 1: Clean Up — Delete Old Code

1. **Delete** `src/app/features/events/event-seating/` directory entirely (4 files: `.ts`, `.html`, `.scss`)
2. **Remove** the `/seating` route from `app.routes.ts`
3. **Revert** the `anyComponentStyle` budget bump in `angular.json` back to `6kB/10kB` (the new SCSS will be much smaller)

---

### Phase 2: Data Layer — Service Interface + Mock Implementation

#### 2a. Create `src/app/core/models/ticket.model.ts` (rewrite, keep existing enums)
- Keep `VenueEventType`, `ZoneCategory`, `ZoneStatus` enums
- Keep `VenueZone`, `TooltipData`, `LegendItem`, `EventConfiguration` interfaces
- **Remove** unused: `FilterState`, `PriceBubble`
- **Remove** `visible` field from `VenueZone` (was always `true`, never read)
- **Remove** `labelAr` from `EventConfiguration` (English-only per requirements)

#### 2b. Create `src/app/application/services/seating-chart.service.ts`
- Interface `ISeatingChartProvider` with `getSeatingConfig(event: Event): Observable<EventConfiguration | null>`
- Class `SeatingChartService` implements `ISeatingChartProvider`
  - `@Injectable({ providedIn: 'root' })`
  - Contains the 3 mock config builders (sport/concert/theater) — moved from component
  - Contains `mapEventType(eventType: string): VenueEventType | null` mapping:
    - `'Sports'` → `Sport`, `'Theater'` → `Theater`, `'Music'` → `Concert`
    - All others → `null` (chart hidden)
  - `getSeatingConfig(event)` calls `mapEventType`, then returns `of(config)` or `of(null)`
  - SVG geometry (path data, bubble positions) stays in the service as well — the component should be purely presentational

#### 2c. Create `src/app/application/abstractions/seating-chart.provider.ts` (optional)
- Move `ISeatingChartProvider` interface here if we want clean separation, but since the project already has interfaces co-located with their consumers, it can live in the service file itself.

---

### Phase 3: New Component — `event-seating-chart`

#### 3a. Create `src/app/shared/components/event-seating-chart/event-seating-chart.component.ts`
- **Standalone**, **OnPush**, **no layout shell** (no navbar/footer)
- `@Input() event: Event | null` (modern signal `input<Event | null>(null)`)
- Inject `SeatingChartService`
- `ngOnChanges` → when `event()` changes, call `service.getSeatingConfig()` → set `config` signal
- If config is `null`, render nothing (chart hidden — the event type doesn't support seating)
- Component is **purely presentational**: receives config, renders SVG, handles tooltip/selection UX
- Expose `@Output() zoneSelected = output<VenueZone>()` so event-detail can react to seat selection

#### 3b. Create `event-seating-chart.component.html`
- Clean 2-column grid: **sidebar filters** (left) + **SVG map** (right)
- Legend rendered inside the sidebar below filters (dynamic — only shows categories present in current config)
- No page title, no navbar, no footer — just the chart widget
- Semantic HTML: `<section>`, `<aside>`, `<figure>` for SVG
- All `@for` control flow (no `*ngFor`)
- All styling via Tailwind classes (matching `event-list` sidebar pattern: `bg-white rounded-2xl shadow-md p-6`) + minimal SCSS for SVG interactions
- No Arabic text in labels (English only)
- Professional SaaS look: clean whitespace, consistent `text-text-primary`/`text-text-secondary` typography

#### 3c. Create `event-seating-chart.component.scss`
- **Minimal**: only SVG zone hover/active transitions, tooltip card, dual-range slider track
- Everything else uses Tailwind (layout, spacing, typography, colors, borders, shadows)
- Target: under 6kB to stay within existing budget

---

### Phase 4: Integration — Embed in Event Details

#### 4a. Update `event-detail.component.ts`
- Import `EventSeatingChartComponent`
- Add to `imports` array

#### 4b. Update `event-detail.component.html`
- After the "Ticket options" table section (after line 163), add:
  ```html
  @if (evt.type === 'Sports' || evt.type === 'Theater' || evt.type === 'Music') {
    <section class="mt-8">
      <h2 class="text-2xl font-bold text-text-primary mb-4">Venue Seating Chart</h2>
      <app-event-seating-chart [event]="evt" (zoneSelected)="onZoneSelected($event)" />
    </section>
  }
  ```
- The type check is a quick guard; the component itself also handles null config from the service

#### 4c. Add `onZoneSelected` handler in `event-detail.component.ts`
- Receives a `VenueZone`, could pre-select the matching ticket type in the booking sidebar

---

### Phase 5: Build Verification
- `ng build` → 0 errors, 0 warnings
- `dotnet build Eventy.WebApi` → 0 errors (31 pre-existing warnings)

---

### Files Summary

| Action | File | Notes |
|--------|------|-------|
| **DELETE** | `features/events/event-seating/` (4 files) | Old standalone page |
| **EDIT** | `app.routes.ts` | Remove `/seating` route |
| **EDIT** | `angular.json` | Revert style budget |
| **REWRITE** | `core/models/ticket.model.ts` | Clean up unused types |
| **CREATE** | `application/services/seating-chart.service.ts` | Service + mock data + mapping |
| **CREATE** | `shared/components/event-seating-chart/event-seating-chart.component.ts` | Presentational component |
| **CREATE** | `shared/components/event-seating-chart/event-seating-chart.component.html` | Clean template |
| **CREATE** | `shared/components/event-seating-chart/event-seating-chart.component.scss` | Minimal SVG-specific styles |
| **EDIT** | `features/events/event-detail/event-detail.component.ts` | Import + import array |
| **EDIT** | `features/events/event-detail/event-detail.component.html` | Embed chart section |