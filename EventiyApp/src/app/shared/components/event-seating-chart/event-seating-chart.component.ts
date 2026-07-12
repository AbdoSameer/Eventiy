import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  HostListener,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Event } from '../../../core/models/event.model';
import {
  EventConfiguration,
  TooltipData,
  VenueEventType,
  VenueZone,
  ZoneCategory,
  ZoneStatus,
} from '../../../core/models/ticket.model';
import { SeatingChartService } from '../../../application/services/seating-chart.service';

/**
 * Reusable, embeddable interactive seating chart.
 *
 * Accepts an {@link Event} via signal input and resolves the matching
 * venue configuration through {@link SeatingChartService}.  The component
 * is purely presentational — all data (zones, prices, geometry, legend)
 * comes from the service, keyed by the event type.
 *
 * Emits {@link zoneSelected} when the user clicks a section so the host
 * page (e.g. event-detail) can react — typically by pre-selecting the
 * matching ticket type in the booking sidebar.
 *
 * The component renders nothing when the event type has no seating
 * configuration (the service returns `null`), so it is safe to embed
 * unconditionally.
 */
@Component({
  selector: 'app-event-seating-chart',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './event-seating-chart.component.html',
  styleUrl: './event-seating-chart.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EventSeatingChartComponent {
  private readonly seatingService = inject(SeatingChartService);
  private readonly destroyRef = inject(DestroyRef);

  // ── Enum references for template binding ──
  readonly VenueEventType = VenueEventType;
  readonly ZoneCategory = ZoneCategory;
  readonly ZoneStatus = ZoneStatus;

  // ── Inputs / outputs ──

  /** The event whose venue should be rendered. */
  readonly event = input<Event | null>(null);

  /** Fired when the user clicks a non-sold-out section. */
  readonly zoneSelected = output<VenueZone>();

  // ── Configuration state (resolved from the service) ──

  readonly config = signal<EventConfiguration | null>(null);
  readonly loading = signal(false);

  // ── Filter state ──

  readonly selectedCategories = signal<Set<ZoneCategory>>(
    new Set([ZoneCategory.Standard, ZoneCategory.Premium, ZoneCategory.VVIP]),
  );
  readonly priceLower = signal(0);
  readonly priceUpper = signal(85_000);

  /** Reset to full bounds whenever a new config loads (excludes inactive zones). */
  readonly priceBounds = computed(() => {
    const zones = this.config()?.zones ?? [];
    const active = zones.filter((z) => z.status !== ZoneStatus.Inactive);
    if (active.length === 0) return { min: 0, max: 85_000 };
    const prices = active.map((z) => z.price);
    return { min: Math.min(...prices), max: Math.max(...prices) };
  });

  // ── Interaction state ──

  readonly hoveredZoneId = signal<string | null>(null);
  readonly selectedZoneId = signal<string | null>(null);
  readonly tooltipVisible = signal(false);
  readonly tooltipX = signal(0);
  readonly tooltipY = signal(0);
  readonly tooltipData = signal<TooltipData | null>(null);

  // ── Derived state ──

  /** Zones that pass the current category + price filters (inactive always excluded). */
  readonly filteredZones = computed<VenueZone[]>(() => {
    const cfg = this.config();
    if (!cfg) return [];
    const cats = this.selectedCategories();
    const lo = this.priceLower();
    const hi = this.priceUpper();
    return cfg.zones.filter((z) => {
      if (z.status === ZoneStatus.Inactive) return false;
      const catOk = cats.size === 0 || cats.has(z.category);
      const priceOk = z.price >= lo && z.price <= hi;
      return catOk && priceOk;
    });
  });

  /** O(1) lookup of visible zone ids for SVG opacity/highlight logic. */
  private readonly visibleIds = computed<Set<string>>(
    () => new Set(this.filteredZones().map((z) => z.id)),
  );

  /** Legend entries from the config (service already trims to present types + adds Inactive). */
  readonly activeLegend = computed(() => this.config()?.legend ?? []);

  /** Currently selected zone (for the detail card). */
  readonly selectedZone = computed<VenueZone | null>(() => {
    const id = this.selectedZoneId();
    const cfg = this.config();
    if (!id || !cfg) return null;
    return cfg.zones.find((z) => z.id === id) ?? null;
  });

  readonly matchCount = computed(() => this.filteredZones().length);

  /** Dual-range slider fill track, expressed as left/right percentages. */
  readonly priceRangeTrack = computed(() => {
    const { min, max } = this.priceBounds();
    const span = max - min || 1;
    const lo = this.priceLower();
    const hi = this.priceUpper();
    return {
      left: ((lo - min) / span) * 100,
      right: 100 - ((hi - min) / span) * 100,
    };
  });

  // ── Config resolution: reload whenever the event input changes ──

  constructor() {
    effect(() => {
      const evt = this.event();
      if (!evt) {
        this.config.set(null);
        return;
      }
      this.loading.set(true);
      this.seatingService
        .getSeatingConfig(evt)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe((cfg) => {
          this.config.set(cfg);
          this.loading.set(false);
          this.resetFilters();
          this.clearSelection();
        });
    });
  }

  // ── Filter handlers ──

  toggleCategory(category: ZoneCategory, checked: boolean): void {
    const next = new Set(this.selectedCategories());
    if (checked) next.add(category);
    else next.delete(category);
    this.selectedCategories.set(next);
  }

  isCategorySelected(category: ZoneCategory): boolean {
    return this.selectedCategories().has(category);
  }

  onLowerPrice(value: string): void {
    const v = Number(value);
    this.priceLower.set(Math.min(v, this.priceUpper()));
  }

  onUpperPrice(value: string): void {
    const v = Number(value);
    this.priceUpper.set(Math.max(v, this.priceLower()));
  }

  resetFilters(): void {
    const { min, max } = this.priceBounds();
    this.selectedCategories.set(
      new Set([ZoneCategory.Standard, ZoneCategory.Premium, ZoneCategory.VVIP]),
    );
    this.priceLower.set(min);
    this.priceUpper.set(max);
  }

  // ── Tooltip + zone interaction ──

  onZoneEnter(zoneId: string): void {
    const zone = this.config()?.zones.find((z) => z.id === zoneId);
    if (!zone) return;
    this.hoveredZoneId.set(zoneId);
    this.tooltipData.set({
      ticketType: zone.ticketType,
      price: zone.price,
      sectionId: zone.id,
      status: zone.status,
      category: zone.category,
      description: zone.description,
      seats: zone.seats,
    });
    this.tooltipVisible.set(true);
  }

  onZoneMove(event: MouseEvent): void {
    this.tooltipX.set(event.clientX + 16);
    this.tooltipY.set(event.clientY + 16);
  }

  onZoneLeave(): void {
    this.hoveredZoneId.set(null);
    this.tooltipVisible.set(false);
    this.tooltipData.set(null);
  }

  onZoneClick(zoneId: string): void {
    const zone = this.config()?.zones.find((z) => z.id === zoneId);
    if (!zone || zone.status === ZoneStatus.SoldOut || zone.status === ZoneStatus.Inactive) return;
    this.selectedZoneId.set(zoneId);
    this.zoneSelected.emit(zone);
  }

  @HostListener('window:resize')
  onResize(): void {
    if (this.tooltipVisible()) this.tooltipVisible.set(false);
  }

  // ── Template helpers ──

  /** Fill colour for a zone; dimmed when filtered out. */
  zoneFill(zoneId: string): string {
    const cfg = this.config();
    if (!cfg) return '#e5e7eb';
    const zone = cfg.zones.find((z) => z.id === zoneId);
    if (!zone) return '#e5e7eb';
    return this.visibleIds().has(zoneId) ? zone.colorCode : '#eef0f2';
  }

  zoneOpacity(zoneId: string): number {
    return this.visibleIds().has(zoneId) ? 1 : 0.35;
  }

  isZoneActive(zoneId: string): boolean {
    return this.hoveredZoneId() === zoneId || this.selectedZoneId() === zoneId;
  }

  isInactiveZone(zoneId: string): boolean {
    return this.config()?.zones?.some(z => z.id === zoneId && z.status === ZoneStatus.Inactive) ?? false;
  }

  formatPrice(price: number): string {
    return '$' + price.toLocaleString('en-US');
  }

  statusClass(status: ZoneStatus): string {
    switch (status) {
      case ZoneStatus.Available:
        return 'sc-status-available';
      case ZoneStatus.LowInventory:
        return 'sc-status-low';
      case ZoneStatus.SoldOut:
        return 'sc-status-soldout';
      case ZoneStatus.Inactive:
        return 'sc-status-inactive';
    }
  }

  trackByZoneId(index: number, zone: VenueZone): string {
    return zone.id;
  }

  trackBySection(index: number, section: { zoneId: string }): string {
    return section.zoneId + '-' + index;
  }

  private clearSelection(): void {
    this.hoveredZoneId.set(null);
    this.selectedZoneId.set(null);
    this.tooltipVisible.set(false);
    this.tooltipData.set(null);
  }

  /** Maps a legend label back to its ZoneCategory for filtering. */
  private legendCategory(label: string): ZoneCategory {
    switch (label) {
      case 'Standard':
        return ZoneCategory.Standard;
      case 'Premium':
        return ZoneCategory.Premium;
      default:
        return ZoneCategory.VVIP;
    }
  }
}
