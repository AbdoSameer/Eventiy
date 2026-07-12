/**
 * Seating chart data models.
 *
 * These interfaces describe the shape of SVG-bound zone data, tooltip
 * payload, and event-type configurations consumed by the
 * EventSeatingChartComponent.  Data is resolved by SeatingChartService
 * based on the active Event — no hardcoded references inside the component.
 */

// ────────────────────────────────────────────────────────────────────
// Enums
// ────────────────────────────────────────────────────────────────────

/** Coarse event classification that determines the venue layout. */
export enum VenueEventType {
  Sport = 'Sport',
  Concert = 'Concert',
  Theater = 'Theater',
}

/** Pricing tier — drives colour coding and legend grouping. */
export enum ZoneCategory {
  Standard = 'Standard',
  Premium = 'Premium',
  VVIP = 'VVIP',
}

/** Live availability shown on the tooltip and the selected-zone card. */
export enum ZoneStatus {
  Available = 'Available',
  LowInventory = 'Low Inventory',
  SoldOut = 'Sold Out',
  Inactive = 'Inactive',
}

// ────────────────────────────────────────────────────────────────────
// Core interfaces
// ────────────────────────────────────────────────────────────────────

/**
 * A single clickable/hoverable section on the SVG venue map.
 * The `id` matches the SVG element id so the graph renderer can bind
 * geometry to data in O(1).
 */
export interface VenueZone {
  /** Unique identifier — matches the SVG element id, e.g. 'S180'. */
  id: string;
  /** Human-readable ticket type label, e.g. 'Premium Club'. */
  ticketType: string;
  /** Base price for one seat in this zone. */
  price: number;
  /** Current availability. */
  status: ZoneStatus;
  /** Pricing tier used for colour coding and legend grouping. */
  category: ZoneCategory;
  /** Pre-computed CSS fill colour based on category + event type. */
  colorCode: string;
  /** Brief description shown in the tooltip and selected-zone card. */
  description: string;
  /** Total seat count (informational). */
  seats: number;
}

/** Payload for the floating tooltip card. */
export interface TooltipData {
  ticketType: string;
  price: number;
  sectionId: string;
  status: ZoneStatus;
  category: ZoneCategory;
  description: string;
  seats: number;
}

/** Legend entry that groups zones by colour. */
export interface LegendItem {
  color: string;
  label: string;
  description: string;
}

/**
 * A single SVG section shape + its label anchor + price-bubble position.
 * The geometry is data, not template markup, so the component stays
 * purely presentational.
 */
export interface SvgSection {
  /** The zone id this shape binds to (looked up in `VenueZone[]`). */
  zoneId: string;
  /** SVG path data. Required when `rect` is absent. */
  d?: string;
  /** Optional rx/ry for rect-based sections (pit, floor, orchestra). */
  rect?: { x: number; y: number; width: number; height: number; rx?: number };
  /** Label anchor coordinates in viewBox units. */
  labelX: number;
  labelY: number;
  /** Whether the label text should be light (for dark fills). */
  lightLabel?: boolean;
}

/** Full configuration for one event-type view. */
export interface EventConfiguration {
  type: VenueEventType;
  /** Display label, e.g. 'Sport (Stadium)'. */
  label: string;
  zones: VenueZone[];
  /** SVG section geometry, bound to zones by id. */
  sections: SvgSection[];
  /** Central element rendered inside the bowl (pitch / stage). */
  centerElement: CenterElement;
  legend: LegendItem[];
}

/** Describes the central element of the venue (pitch or stage). */
export interface CenterElement {
  kind: 'pitch' | 'stage';
  label: string;
}
