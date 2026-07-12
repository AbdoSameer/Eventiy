import { Injectable } from '@angular/core';
import {
  BlockGroup,
  SeatNode,
  SeatStatus,
  StageFloorPit,
  VenueGraphData,
  ZoneConfig,
} from '../models/venue-graph.interfaces';

/**
 * Math projection layer.
 *
 * Builds an entire VenueGraphData on the fly using polar→cartesian
 * transformations for SPORT mode (concentric stadium arcs) and
 * polygon generators for CONCERT mode (floor pits & stage).
 *
 * Coordinate space is the SVG viewBox: 1000 × 800 with the venue
 * center at (500, 450).
 */
@Injectable({ providedIn: 'root' })
export class CoordinateTransformerService {
  /** SVG center. */
  private readonly cx = 500;
  private readonly cy = 450;

  /**
   * Top-level entry point. Returns a complete, flattened graph the
   * renderer can consume in either mode.
   */
  public generateMockVenueData(): VenueGraphData {
    const zones: ZoneConfig[] = [
      this.createCurvedZone(
        'VIP_ZONE',
        'VIP Premium Club',
        '--color-vip-zone',
        180,
        240,
        4,
        25,
        -150,
        -30,
      ),
      this.createCurvedZone(
        'SIDE_BOWL_L',
        'Side Bowl Left',
        '--color-side-bowl',
        300,
        340,
        6,
        35,
        100,
        260,
      ),
      this.createCurvedZone(
        'SIDE_BOWL_R',
        'Side Bowl Right',
        '--color-side-bowl',
        300,
        340,
        6,
        35,
        -80,
        80,
      ),
    ];

    const floorPits: StageFloorPit[] = [
      {
        id: 'PIT_VVIP',
        name: 'Front Pit VVIP',
        polygonPoints: '400,320 600,320 650,420 350,420',
        price: 1250,
        status: 'FAST_SELLING',
        colorToken: '--color-vvip-pit',
      },
      {
        id: 'PIT_FLOOR',
        name: 'General Floor Pit',
        polygonPoints: '350,430 650,430 700,560 300,560',
        price: 350,
        status: 'AVAILABLE',
        colorToken: '--color-floor-pit',
      },
    ];

    return {
      venueId: 'STADIUM_ST_001',
      venueName: 'Eventy Arena International',
      zones,
      floorPits,
    };
  }

  /**
   * Two-Phase Architecture – Phase 1: Design-time.
   * Build a curved amphitheater zone using polar→cartesian math.
   *
   *     x = cx + r · cos(θ)
   *     y = cy + r · sin(θ)
   */
  private createCurvedZone(
    zoneId: string,
    zoneName: string,
    colorToken: string,
    innerRadius: number,
    outerRadius: number,
    rowsCount: number,
    seatsPerRow: number,
    startAngleDeg: number,
    endAngleDeg: number,
  ): ZoneConfig {
    const blocks: BlockGroup[] = [];

    // Split the arc into two blocks so we get realistic seat chunks.
    const midAngle = (startAngleDeg + endAngleDeg) / 2;
    const angles = [
      { name: 'Block A', start: startAngleDeg, end: midAngle },
      { name: 'Block B', start: midAngle, end: endAngleDeg },
    ];

    angles.forEach((blockConfig, blockIndex) => {
      const seats: SeatNode[] = [];
      const rowSpacing = (outerRadius - innerRadius) / rowsCount;

      for (let r = 0; r < rowsCount; r++) {
        const currentRadius = innerRadius + r * rowSpacing;
        const rowLetter = String.fromCharCode(65 + r); // A, B, C …

        for (let s = 0; s < seatsPerRow; s++) {
          const angleStep =
            (blockConfig.end - blockConfig.start) / Math.max(seatsPerRow - 1, 1);
          const currentAngleDeg = blockConfig.start + s * angleStep;
          const angleRad = (currentAngleDeg * Math.PI) / 180;

          const cx = this.cx + currentRadius * Math.cos(angleRad);
          const cy = this.cy + currentRadius * Math.sin(angleRad);

          // +90° so each seat faces the pitch (assuming "up" = facing center).
          const rotationAngle = currentAngleDeg + 90;

          seats.push({
            id: `${zoneId}_B${blockIndex}_R${rowLetter}_S${s}`,
            rowLabel: rowLetter,
            seatNumber: s + 1,
            cx,
            cy,
            rotationAngle,
            status: this.getRandomStatus(s),
            price: zoneId === 'VIP_ZONE' ? 6850 : 2294,
            category: zoneName,
            tier: zoneId === 'VIP_ZONE' ? 'premium' : 'standard',
          });
        }
      }

      // Calculate boundary path for the block
      // We add some padding around the seats so the path bounds the whole block
      const padding = rowSpacing * 0.5;
      const innerRad = innerRadius - padding;
      const outerRad = outerRadius + padding;
      // Convert start and end angles to radians, add slight padding
      const anglePad = 1.5;
      const startRad = ((blockConfig.start - anglePad) * Math.PI) / 180;
      const endRad = ((blockConfig.end + anglePad) * Math.PI) / 180;
      const largeArc = (blockConfig.end - blockConfig.start + 2 * anglePad) > 180 ? 1 : 0;

      const p1x = this.cx + innerRad * Math.cos(startRad);
      const p1y = this.cy + innerRad * Math.sin(startRad);
      const p2x = this.cx + outerRad * Math.cos(startRad);
      const p2y = this.cy + outerRad * Math.sin(startRad);
      const p3x = this.cx + outerRad * Math.cos(endRad);
      const p3y = this.cy + outerRad * Math.sin(endRad);
      const p4x = this.cx + innerRad * Math.cos(endRad);
      const p4y = this.cy + innerRad * Math.sin(endRad);

      const boundaryPath = `M ${p1x},${p1y} L ${p2x},${p2y} A ${outerRad},${outerRad} 0 ${largeArc} 1 ${p3x},${p3y} L ${p4x},${p4y} A ${innerRad},${innerRad} 0 ${largeArc} 0 ${p1x},${p1y} Z`;

      blocks.push({
        id: `${zoneId}_B${blockIndex}`,
        name: `${zoneName} - ${blockConfig.name}`,
        seats,
        boundaryPath,
      });
    });

    return { id: zoneId, name: zoneName, colorToken, blocks };
  }

  /**
   * Two-Phase Architecture – Phase 2: Flattening.
   * The "Save Venue" operation. Converts the curved generator output
   * into concrete, hardcoded spatial points (x, y, θ) for backend dispatch.
   * Returns a JSON string ready for transport.
   */
  public flattenVenueToBackend(data: VenueGraphData): string {
    const flattened = {
      venueId: data.venueId,
      venueName: data.venueName,
      generatedAt: new Date().toISOString(),
      seats: data.zones.flatMap((z) =>
        z.blocks.flatMap((b) =>
          b.seats.map((s) => ({
            id: s.id,
            x: Math.round(s.cx * 100) / 100,
            y: Math.round(s.cy * 100) / 100,
            theta: s.rotationAngle,
            price: s.price,
            category: s.category,
            blockId: b.id,
            zoneId: z.id,
          })),
        ),
      ),
      floorPits: (data.floorPits ?? []).map((p) => ({
        id: p.id,
        name: p.name,
        polygon: p.polygonPoints,
        price: p.price,
      })),
    };

    return JSON.stringify(flattened);
  }

  private getRandomStatus(index: number): SeatStatus {
    if (index % 11 === 0) return 'RESERVED';
    if (index % 7 === 0) return 'FAST_SELLING';
    return 'AVAILABLE';
  }
}
