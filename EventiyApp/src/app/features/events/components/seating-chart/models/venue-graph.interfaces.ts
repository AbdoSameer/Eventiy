/**
 * Strongly-typed contracts for the Eventy Engine.
 *
 * These models treat the venue as a mathematical Graph:
 *   [EventType] -> [VenueGraphData] -> [ZoneConfig] -> [BlockGroup] -> [SeatNode]
 *                                                -> [StageFloorPit]  (CONCERT mode only)
 */

export type VenueMode = 'SPORT' | 'CONCERT';

export type SeatStatus = 'AVAILABLE' | 'RESERVED' | 'FAST_SELLING' | 'SELECTED';

export interface SeatNode {
  id: string;
  rowLabel: string;
  seatNumber: number;
  cx: number;
  cy: number;
  /** In degrees, oriented so each seat faces the center of the venue. */
  rotationAngle: number;
  status: SeatStatus;
  price: number;
  category: string;
  /** Optional grouping for filtering (e.g. tier: 'premium' | 'standard'). */
  tier?: 'premium' | 'standard' | 'economy';
}

export interface BlockGroup {
  id: string;
  name: string;
  seats: SeatNode[];
  /** Optional SVG path that bounds the entire block (used for hover & clipping). */
  boundaryPath?: string;
}

export interface ZoneConfig {
  id: string;
  name: string;
  /** Pointer to a CSS custom property, e.g. '--color-vip-zone'. */
  colorToken: string;
  blocks: BlockGroup[];
}

export interface StageFloorPit {
  id: string;
  name: string;
  /** SVG polygon "x1,y1 x2,y2 …" – supports irregular shapes. */
  polygonPoints: string;
  price: number;
  status: SeatStatus;
  /** Pointer to a CSS custom property, e.g. '--color-vvip-pit'. */
  colorToken: string;
}

export interface VenueGraphData {
  venueId: string;
  venueName: string;
  zones: ZoneConfig[];
  floorPits?: StageFloorPit[];
}

/** Union of anything that can be hovered on the canvas. */
export type HoverTarget =
  | { kind: 'seat'; payload: SeatNode }
  | { kind: 'pit'; payload: StageFloorPit }
  | { kind: 'block'; payload: BlockGroup }
  | { kind: 'none' };

/** Filter shape for the right-hand filter panel. */
export interface VenueFilterState {
  /** Section boundary filter – restrict to specific block ids. */
  blockIds: string[];
  /** Inclusive price range. */
  priceMin: number;
  priceMax: number;
  /** Show only certain statuses. Empty array = show all. */
  statuses: SeatStatus[];
}

export const DEFAULT_VENUE_FILTER: VenueFilterState = {
  blockIds: [],
  priceMin: 0,
  priceMax: Number.POSITIVE_INFINITY,
  statuses: [],
};
