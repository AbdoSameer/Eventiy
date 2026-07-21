import { Injectable, computed, signal } from '@angular/core';
import {
  DEFAULT_VENUE_FILTER,
  SeatNode,
  SeatStatus,
  VenueFilterState,
  VenueGraphData,
  VenueMode,
} from '../models/venue-graph.interfaces';
import { CoordinateTransformerService } from '../renderer/coordinate-transformer.service';

/**
 * Component-level state controller for the seating chart.
 *
 * Encapsulates every piece of mutable state behind Angular Signals:
 *   - mode:            SPORT | CONCERT
 *   - selectedSeats:   Set of seat ids the user has clicked
 *   - hover:           {x, y, target} for the tooltip overlay
 *   - filter:          {blockIds, priceMin, priceMax, statuses}
 *
 * `visibleSeats` is a computed that automatically re-derives whenever
 * any input signal changes, so the consumer never has to re-filter.
 */
@Injectable({ providedIn: 'root' })
export class VenueViewStore {
  // ── State ────────────────────────────────────────────────
  private readonly _mode = signal<VenueMode>('SPORT');
  private readonly _venueData = signal<VenueGraphData | null>(null);
  private readonly _selectedSeatIds = signal<Set<string>>(new Set());
  private readonly _filter = signal<VenueFilterState>({ ...DEFAULT_VENUE_FILTER });

  // ── Public read-only signals ─────────────────────────────
  readonly mode = this._mode.asReadonly();
  readonly venueData = this._venueData.asReadonly();
  readonly selectedSeatIds = this._selectedSeatIds.asReadonly();
  readonly filter = this._filter.asReadonly();

  // ── Computed views ───────────────────────────────────────

  /** Flatten + filter seats based on the current filter state. */
  readonly visibleSeats = computed<SeatNode[]>(() => {
    const data = this._venueData();
    if (!data) return [];
    const f = this._filter();
    const allSeats = data.zones.flatMap((z) => z.blocks.flatMap((b) => b.seats));

    return allSeats.filter((s) => {
      if (f.statuses.length > 0 && !f.statuses.includes(s.status)) return false;
      if (s.price < f.priceMin || s.price > f.priceMax) return false;
      if (f.blockIds.length > 0) {
        // The seat's block id is encoded in its id: <zone>_B<n>_R<r>_S<n>
        const seatBlockId = s.id.split('_R')[0]; // → <zone>_B<n>
        if (!f.blockIds.includes(seatBlockId)) return false;
      }
      return true;
    });
  });

  /** Prices per visible block – drives the floating price badges. */
  readonly blockSummaries = computed(() => {
    const data = this._venueData();
    if (!data) return [] as { blockId: string; blockName: string; price: number; x: number; y: number; status: SeatStatus }[];

    const result: { blockId: string; blockName: string; price: number; x: number; y: number; status: SeatStatus }[] = [];
    data.zones.forEach((z) =>
      z.blocks.forEach((b) => {
        if (!b.seats.length) return;
        // Centroid of block for badge placement
        const sumX = b.seats.reduce((acc, s) => acc + s.cx, 0);
        const sumY = b.seats.reduce((acc, s) => acc + s.cy, 0);
        const x = sumX / b.seats.length;
        const y = sumY / b.seats.length;
        // Use the median-ish price (first seat price) for the badge.
        const price = b.seats[0].price;
        let status: SeatStatus = 'RESERVED';
        if (b.seats.some((s) => s.status === 'FAST_SELLING')) {
          status = 'FAST_SELLING';
        } else if (b.seats.some((s) => s.status === 'AVAILABLE')) {
          status = 'AVAILABLE';
        }
        result.push({ blockId: b.id, blockName: b.name, price, x, y, status });
      }),
    );
    return result;
  });

  readonly totalSelected = computed(() => this._selectedSeatIds().size);

  /**
   * Full metadata of every currently-selected seat. Derived
   * purely from the venue data + selection set, so the consumer
   * (cart sidebar, totals, etc.) never has to re-walk the graph.
   */
  readonly selectedSeatsMetadata = computed<SeatNode[]>(() => {
    const data = this._venueData();
    if (!data) return [];
    const ids = this._selectedSeatIds();
    const out: SeatNode[] = [];
    data.zones.forEach((z) =>
      z.blocks.forEach((b) =>
        b.seats.forEach((s) => {
          if (ids.has(s.id)) out.push(s);
        }),
      ),
    );
    return out;
  });

  readonly totalCartPrice = computed(() => {
    return this.selectedSeatsMetadata().reduce((acc, s) => acc + s.price, 0);
  });

  readonly totalPrice = computed(() => this.totalCartPrice());

  // ── Mutators ─────────────────────────────────────────────

  loadVenue(transformer: CoordinateTransformerService): void {
    this._venueData.set(transformer.generateMockVenueData());
  }

  setMode(mode: VenueMode): void {
    if (this._mode() === mode) return;
    this._mode.set(mode);
  }

  toggleSeat(seatId: string): boolean {
    const next = new Set(this._selectedSeatIds());
    if (next.has(seatId)) {
      next.delete(seatId);
    } else {
      next.add(seatId);
    }
    this._selectedSeatIds.set(next);
    return next.has(seatId);
  }

  isSelected(seatId: string): boolean {
    return this._selectedSeatIds().has(seatId);
  }

  clearSelection(): void {
    this._selectedSeatIds.set(new Set());
  }

  /**
   * Force-reset every piece of internal state. Called by the
   * orchestrator BEFORE loading the alternative geometric
   * coordinate map during a SPORT ⇄ CONCERT mode switch.
   */
  resetForModeSwitch(): void {
    this._selectedSeatIds.set(new Set());
    this._filter.set({ ...DEFAULT_VENUE_FILTER });
  }

  /**
   * Apply a single server delta to the in-memory graph and
   * return the seat (or null) so the orchestrator can decide
   * whether to flash the node. We mutate the seat's status
   * field IN-PLACE rather than re-creating the array, which
   * keeps Signal subscriptions stable.
   */
  applyServerDelta(seatId: string, status: SeatStatus): SeatNode | null {
    const data = this._venueData();
    if (!data) return null;

    let updatedSeat: SeatNode | null = null;

    const newZones = data.zones.map(z => ({
      ...z,
      blocks: z.blocks.map(b => ({
        ...b,
        seats: b.seats.map(s => {
          if (s.id === seatId) {
            updatedSeat = { ...s, status };
            return updatedSeat;
          }
          return s;
        }),
      })),
    }));

    if (!updatedSeat) return null;

    this._venueData.set({ ...data, zones: newZones });
    return updatedSeat;
  }

  setFilter(partial: Partial<VenueFilterState>): void {
    this._filter.update((prev) => ({ ...prev, ...partial }));
  }
}
