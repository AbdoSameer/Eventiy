import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { Event, TicketDetailsDto } from '../../core/models/event.model';
import {
  CenterElement,
  EventConfiguration,
  LegendItem,
  SvgSection,
  VenueEventType,
  VenueZone,
  ZoneCategory,
  ZoneStatus,
} from '../../core/models/ticket.model';

export interface ISeatingChartProvider {
  getSeatingConfig(event: Event): Observable<EventConfiguration | null>;
}

const COLOR = {
  standard: '#4ade80',
  premium: '#fb923c',
  vvipDark: '#18181b',
  vvipPurple: '#7c3aed',
  inactive: '#d1d5db',
} as const;

const INACTIVE_ZONE: Omit<VenueZone, 'id' | 'description'> = {
  ticketType: 'Unavailable',
  price: 0,
  status: ZoneStatus.Inactive,
  category: ZoneCategory.Standard,
  colorCode: COLOR.inactive,
  seats: 0,
};

@Injectable({ providedIn: 'root' })
export class SeatingChartService implements ISeatingChartProvider {
  private readonly cache = new Map<string, EventConfiguration | null>();

  getSeatingConfig(event: Event): Observable<EventConfiguration | null> {
    const cached = this.cache.get(event.id);
    if (cached !== undefined) {
      return of(cached);
    }

    const venueType = this.mapEventType(event.type);
    const config = venueType && event.ticketTypes
      ? this.buildConfig(venueType, event.ticketTypes)
      : null;
    this.cache.set(event.id, config);
    return of(config);
  }

  private mapEventType(eventType: string): VenueEventType | null {
    switch (eventType) {
      case 'Sports':
        return VenueEventType.Sport;
      case 'Music':
        return VenueEventType.Concert;
      case 'Theater':
        return VenueEventType.Theater;
      default:
        return null;
    }
  }

  private buildConfig(type: VenueEventType, tickets: TicketDetailsDto[]): EventConfiguration {
    switch (type) {
      case VenueEventType.Sport:
        return this.buildSportConfig(tickets);
      case VenueEventType.Concert:
        return this.buildConcertConfig(tickets);
      case VenueEventType.Theater:
        return this.buildTheaterConfig(tickets);
    }
  }

  private zoneFromTicket(ticket: TicketDetailsDto, category: ZoneCategory, colorCode: string, description: string): VenueZone {
    const available = ticket.capacity;
    const status = available <= 0
      ? ZoneStatus.SoldOut
      : available < 100
        ? ZoneStatus.LowInventory
        : ZoneStatus.Available;
    return {
      id: ticket.sectionCode ?? ticket.id,
      ticketType: ticket.name,
      price: ticket.price,
      status,
      category,
      colorCode,
      description,
      seats: available,
    };
  }

  // ──────────────────────────────────────────────────────────────
  // Sport — stadium bowl with green pitch
  // ──────────────────────────────────────────────────────────────

  private buildSportConfig(tickets: TicketDetailsDto[]): EventConfiguration {
    const zones: VenueZone[] = [];

    const add = (code: string, desc: string) => {
      const t = tickets.find(x => x.sectionCode === code);
      if (!t) {
        zones.push({ id: code, ...INACTIVE_ZONE, description: 'No ticket type assigned.' });
        return;
      }
      const cat = t.price >= 40000 ? ZoneCategory.VVIP : t.price >= 4000 ? ZoneCategory.Premium : ZoneCategory.Standard;
      const color = cat === ZoneCategory.VVIP ? COLOR.vvipDark : cat === ZoneCategory.Premium ? COLOR.premium : COLOR.standard;
      zones.push({ ...this.zoneFromTicket(t, cat, color, desc) });
    };

    add('116', 'Upper-tier end-zone seats with a panoramic view of the pitch.');
    add('124L', 'Lower-bowl sideline with great sightlines for the main action.');
    add('234', 'Corner stand near the supporter sections — energetic atmosphere.');
    add('S105', 'Mid-tier side section with a balanced view of both goals.');
    add('S118', 'Side stand with covered roofing and quick concourse access.');
    add('S180', 'Club-level sideline with padded seats, lounge access, and in-seat service.');
    add('C129', 'Climate-controlled lounge with gourmet catering and an open bar.');
    add('GC19', 'Private corner suite with a dedicated host and refreshments.');
    add('VVIP1', 'Exclusive owners-style skybox at midfield with a private entrance.');
    add('VVIP2', 'Glass-fronted suite on the halfway line with valet parking.');

    const sections: SvgSection[] = [
      this.pathSection('116', 'M120 40 L260 40 L240 150 L150 150 Z', 200, 100),
      this.pathSection('124L', 'M280 40 L440 40 L430 150 L250 150 Z', 350, 100),
      this.pathSection('234', 'M460 40 L620 40 L630 150 L450 150 Z', 540, 100),
      this.pathSection('S118', 'M640 40 L800 40 L820 150 L650 150 Z', 730, 100),
      this.pathSection('S105', 'M820 40 L960 40 L950 150 L840 150 Z', 890, 100),
      this.pathSection('116', 'M150 590 L240 590 L260 700 L120 700 Z', 200, 650),
      this.pathSection('124L', 'M250 590 L430 590 L440 700 L280 700 Z', 350, 650),
      this.pathSection('234', 'M450 590 L630 590 L620 700 L460 700 Z', 540, 650),
      this.pathSection('S118', 'M650 590 L820 590 L800 700 L640 700 Z', 730, 650),
      this.pathSection('S105', 'M840 590 L950 590 L960 700 L820 700 Z', 890, 650),
      this.pathSection('S180', 'M40 160 L160 160 L160 580 L40 580 Q30 370 40 160 Z', 100, 370),
      this.pathSection('C129', 'M160 200 L290 200 L290 540 L160 540 Z', 225, 370),
      this.pathSection('GC19', 'M840 200 L960 200 Q970 370 960 580 L840 580 Z', 900, 370),
      this.pathSection('VVIP1', 'M300 175 L470 175 L470 230 L300 230 Z', 385, 207, true),
      this.pathSection('VVIP2', 'M530 175 L700 175 L700 230 L530 230 Z', 615, 207, true),
    ];

    const legend = this.buildLegend(zones, [
      { color: COLOR.standard, label: 'Standard', description: 'Upper and lower bowl — great value general seating.' },
      { color: COLOR.premium, label: 'Premium', description: 'Club level — padded seats, lounge, in-seat service.' },
      { color: COLOR.vvipDark, label: 'VVIP', description: 'Private skyboxes — catering, host, valet parking.' },
    ]);

    return { type: VenueEventType.Sport, label: 'Sport (Stadium)', zones, sections, centerElement: { kind: 'pitch', label: 'PITCH' }, legend };
  }

  // ──────────────────────────────────────────────────────────────
  // Concert — stage + standing pit + floor seating
  // ──────────────────────────────────────────────────────────────

  private buildConcertConfig(tickets: TicketDetailsDto[]): EventConfiguration {
    const zones: VenueZone[] = [];

    const add = (code: string, desc: string, cat: ZoneCategory, color: string) => {
      const t = tickets.find(x => x.sectionCode === code);
      if (!t) {
        zones.push({ id: code, ...INACTIVE_ZONE, description: 'No ticket type assigned.' });
        return;
      }
      zones.push({ ...this.zoneFromTicket(t, cat, color, desc) });
    };

    add('FP1', 'Standing pit directly in front of the stage — closest to the artist.', ZoneCategory.VVIP, COLOR.vvipPurple);
    add('FP2', 'Side-front pit with a slightly angled view of the stage.', ZoneCategory.VVIP, COLOR.vvipPurple);
    add('MF1', 'Reserved floor seating behind the pit, flat level.', ZoneCategory.Premium, COLOR.premium);
    add('MF2', 'Rear floor seating with an elevated sightline over the pit.', ZoneCategory.Premium, COLOR.premium);
    add('SB1', 'Lower-bowl side sections with full stage visibility.', ZoneCategory.Standard, COLOR.standard);
    add('SB2', 'Upper-bowl side — great value for the full production.', ZoneCategory.Standard, COLOR.standard);
    add('REAR', 'Behind-stage-adjacent seating with a partial view.', ZoneCategory.Standard, COLOR.standard);
    add('VIP1', 'Private balcony suite with lounge, dedicated bar, and catering.', ZoneCategory.VVIP, COLOR.vvipDark);
    add('VIP2', 'Front-row balcony with unobstructed stage view and waiter service.', ZoneCategory.VVIP, COLOR.vvipDark);

    const sections: SvgSection[] = [
      this.pathSection('SB1', 'M120 40 L300 40 L280 150 L150 150 Z', 210, 100),
      this.pathSection('SB2', 'M320 40 L500 40 L500 150 L300 150 Z', 410, 100),
      this.pathSection('REAR', 'M520 40 L700 40 L700 150 L520 150 Z', 610, 100),
      this.pathSection('VIP2', 'M720 40 L900 40 L920 150 L720 150 Z', 820, 100, true),
      this.pathSection('SB1', 'M150 590 L300 590 L280 700 L120 700 Z', 210, 650),
      this.pathSection('SB2', 'M320 590 L500 590 L500 700 L300 700 Z', 410, 650),
      this.pathSection('VIP1', 'M720 590 L900 590 L920 700 L700 700 Z', 820, 650, true),
      this.pathSection('SB1', 'M40 160 L150 160 L150 580 L40 580 Z', 95, 370),
      this.pathSection('SB2', 'M900 160 L960 160 L960 580 L900 580 Z', 930, 370),
      this.rectSection('FP2', 300, 355, 55, 50, 6, 327, 385, true),
      this.rectSection('FP1', 360, 355, 280, 50, 8, 500, 385, true),
      this.rectSection('FP2', 645, 355, 55, 50, 6, 0, 0, true),
      this.rectSection('MF1', 360, 420, 280, 45, 6, 500, 448),
      this.rectSection('MF2', 360, 475, 280, 40, 6, 500, 500),
    ];

    const legend = this.buildLegend(zones, [
      { color: COLOR.standard, label: 'Standard', description: 'Bowl seating — clear views at accessible prices.' },
      { color: COLOR.premium, label: 'Premium', description: 'Reserved floor seats behind the pit.' },
      { color: COLOR.vvipPurple, label: 'VIP Pit', description: 'Front-pit standing — closest to the stage.' },
      { color: COLOR.vvipDark, label: 'VVIP Suite', description: 'Private suites with catering and waiter service.' },
    ]);

    return { type: VenueEventType.Concert, label: 'Concert (Theater)', zones, sections, centerElement: { kind: 'stage', label: 'MAIN STAGE' }, legend };
  }

  // ──────────────────────────────────────────────────────────────
  // Theater — proscenium stage + orchestra + boxes
  // ──────────────────────────────────────────────────────────────

  private buildTheaterConfig(tickets: TicketDetailsDto[]): EventConfiguration {
    const zones: VenueZone[] = [];

    const add = (code: string, desc: string, cat: ZoneCategory, color: string) => {
      const t = tickets.find(x => x.sectionCode === code);
      if (!t) {
        zones.push({ id: code, ...INACTIVE_ZONE, description: 'No ticket type assigned.' });
        return;
      }
      zones.push({ ...this.zoneFromTicket(t, cat, color, desc) });
    };

    add('ORCH', 'Prime orchestra seating in the centre of the stalls.', ZoneCategory.Premium, COLOR.premium);
    add('ORCHL', 'Left orchestra stalls with a slight angle toward the stage.', ZoneCategory.Standard, COLOR.standard);
    add('ORCHR', 'Right orchestra stalls with a slight angle toward the stage.', ZoneCategory.Standard, COLOR.standard);
    add('MEZZ', 'Elevated mezzanine with a full, unobstructed overview.', ZoneCategory.Standard, COLOR.standard);
    add('BALC', 'Upper balcony — best value with a wide stage panorama.', ZoneCategory.Standard, COLOR.standard);
    add('BOXL', 'Private box on house left with a dedicated attendant.', ZoneCategory.VVIP, COLOR.vvipPurple);
    add('BOXR', 'Private box on house right with a dedicated attendant.', ZoneCategory.VVIP, COLOR.vvipPurple);
    add('FRONT', 'Front-row orchestra, flush with the stage lip.', ZoneCategory.Premium, COLOR.premium);

    const sections: SvgSection[] = [
      this.pathSection('BALC', 'M120 40 L880 40 L860 160 L140 160 Z', 500, 110),
      this.pathSection('MEZZ', 'M160 175 L840 175 L820 280 L180 280 Z', 500, 235),
      this.pathSection('BOXL', 'M40 180 L150 180 L150 560 L40 560 Z', 95, 370, true),
      this.pathSection('BOXR', 'M850 180 L960 180 L960 560 L850 560 Z', 905, 370, true),
      this.pathSection('BALC', 'M140 580 L860 580 L880 700 L120 700 Z', 500, 645),
      this.rectSection('FRONT', 380, 375, 240, 20, 4, 500, 389),
      this.rectSection('ORCH', 380, 400, 240, 80, 6, 500, 445),
      this.rectSection('ORCHL', 300, 400, 70, 80, 6, 335, 445),
      this.rectSection('ORCHR', 630, 400, 70, 80, 6, 665, 445),
    ];

    const legend = this.buildLegend(zones, [
      { color: COLOR.standard, label: 'Standard', description: 'Side orchestra, mezzanine, and balcony.' },
      { color: COLOR.premium, label: 'Premium', description: 'Centre orchestra and front row.' },
      { color: COLOR.vvipPurple, label: 'Box Suite', description: 'Private boxes with attendant service.' },
    ]);

    return { type: VenueEventType.Theater, label: 'Theater (Stage)', zones, sections, centerElement: { kind: 'stage', label: 'STAGE' }, legend };
  }

  // ── Factory helpers ──

  private pathSection(zoneId: string, d: string, labelX: number, labelY: number, lightLabel = false): SvgSection {
    return { zoneId, d, labelX, labelY, lightLabel };
  }

  private rectSection(
    zoneId: string, x: number, y: number, width: number, height: number, rx: number,
    labelX: number, labelY: number, lightLabel = false,
  ): SvgSection {
    return { zoneId, rect: { x, y, width, height, rx }, labelX, labelY, lightLabel };
  }

  /** Appends an Inactive legend entry if any inactive zones exist. */
  private buildLegend(zones: VenueZone[], base: LegendItem[]): LegendItem[] {
    if (zones.some(z => z.status === ZoneStatus.Inactive)) {
      return [...base, { color: COLOR.inactive, label: 'Inactive', description: 'No ticket type assigned to this section.' }];
    }
    return base;
  }
}
