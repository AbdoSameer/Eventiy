import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  ViewChild,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';

import { VenueViewStore } from './state/venue-view.store';
import { CoordinateTransformerService } from './renderer/coordinate-transformer.service';
import { VenueGraphRendererService } from './renderer/venue-graph-renderer.service';
import { LiveStateSyncService } from './renderer/live-state-sync.service';
import { SeatLockOrchestratorService } from './state/seat-lock-orchestrator.service';

import { environment } from '../../../../../environments/environment';

import { EventApplicationService } from '../../../../application/services/event-application.service';
import { BookingApplicationService } from '../../../../application/services/booking-application.service';
import { Event } from '../../../../core/models/event.model';
import { SeatNode, StageFloorPit, VenueMode, BlockGroup } from './models/venue-graph.interfaces';
import { SeatingTooltipComponent } from './ui-elements/seating-tooltip.component';
import { PriceBadgeComponent } from './ui-elements/price-badge.component';

const VIEW_WIDTH = 1000;
const VIEW_HEIGHT = 800;

interface ToastItem {
  id: number;
  kind: 'info' | 'warn' | 'error' | 'success';
  message: string;
}

/**
 * Main Architecture Orchestrator.
 *
 * Wires together: store + renderer + live sync + lock orchestrator.
 * Owns the SVG lifecycle, drives mode switches (with the
 * purge-first protocol), and surfaces collision toasts.
 */
@Component({
  selector: 'app-seating-chart',
  standalone: true,
  imports: [CommonModule, RouterLink, SeatingTooltipComponent, PriceBadgeComponent],
  templateUrl: './seating-chart.component.html',
  styleUrls: ['./seating-chart.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeatingChartComponent implements AfterViewInit, OnDestroy {
  // ── Route param (via withComponentInputBinding) ──
  readonly id = input<string>('');

  // ── DI ──
  readonly store = inject(VenueViewStore);
  private readonly transformer = inject(CoordinateTransformerService);
  private readonly renderer = inject(VenueGraphRendererService);
  private readonly liveSync = inject(LiveStateSyncService);
  private readonly lockOrchestrator = inject(SeatLockOrchestratorService);
  private readonly eventApp = inject(EventApplicationService);
  private readonly bookingApp = inject(BookingApplicationService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  @ViewChild('svgRoot', { static: true })
  private svgRoot!: ElementRef<SVGSVGElement>;

  // ── View-only state ──
  readonly viewWidth = VIEW_WIDTH;
  readonly viewHeight = VIEW_HEIGHT;

  readonly tooltipX = signal(0);
  readonly tooltipY = signal(0);
  readonly tooltipPayload = signal<SeatNode | StageFloorPit | null>(null);
  readonly tooltipKind = signal<'seat' | 'pit' | 'none'>('none');

  /** Micro-notifications surfaced from collision / lock events. */
  readonly toasts = signal<ToastItem[]>([]);
  private toastId = 0;

  /**
   * Prevents concurrent map interactions while a checkout
   * LOCK sequence is in-flight over the WebSocket.
   */
  readonly submitting = signal(false);

  readonly statusBreakdown = computed(() => {
    const data = this.store.venueData();
    if (!data) return [];
    const all = data.zones.flatMap((z) => z.blocks.flatMap((b) => b.seats));
    const counts: Record<string, number> = {};
    all.forEach((s) => (counts[s.status] = (counts[s.status] ?? 0) + 1));
    return Object.entries(counts).map(([status, count]) => ({ status, count }));
  });

  readonly containerHeight = signal(640);
  readonly containerWidth = signal(0);
  readonly zoomTransform = signal<{ x: number; y: number; k: number }>({ x: 0, y: 0, k: 1 });

  /** Live-sync indicators exposed to the toolbar. */
  readonly syncStatus = this.liveSync.status;
  readonly lastDeltaAt = this.liveSync.lastDeltaAt;
  readonly pendingLockCount = computed(() => this.lockOrchestrator.pendingLocks().size);

  readonly eventData = signal<Event | null>(null);
  readonly selectedTicketTypeId = signal('');
  readonly paymentMethod = signal<'Instant' | 'Deferred'>('Instant');

  /**
   * Real booking total derived from the ticket type's unit price × quantity.
   * This matches what the backend will charge (ticketType.Price * quantity),
   * unlike store.totalCartPrice() which sums individual mock seat prices
   * that may differ from the actual ticket type price.
   */
  readonly bookingTotal = computed(() => {
    const evt = this.eventData();
    const qty = this.store.totalSelected();
    if (!evt || qty === 0) return 0;
    const ticketTypeId = this.selectedTicketTypeId();
    const ticket = evt.ticketTypes?.find((t) => t.id === ticketTypeId);
    return ticket ? ticket.price * qty : 0;
  });

  private readonly destroy$ = new Subject<void>();

  constructor() {
    // 1. Hydrate venue data
    this.store.loadVenue(this.transformer);

    // 2. React to mode changes by re-rendering the canvas.
    effect(() => {
      const data = this.store.venueData();
      const mode = this.store.mode();
      if (!data) return;
      // Set lock orchestrator context from venue data
      this.lockOrchestrator.setContext(this.id() || data.venueId, '');
      queueMicrotask(() => {
        if (!this.svgRoot) return;
        this.renderer.renderVenue(
          data,
          mode,
          (seat) => this.handleSeatClick(seat),
          (pit) => this.handlePitClick(pit),
          (block) => this.handleBlockClick(block),
          (event, target) => this.handleHover(event, target),
        );
      });
    });
  }

  ngAfterViewInit(): void {
    this.renderer.initializeEngine(
      this.svgRoot,
      VIEW_WIDTH,
      VIEW_HEIGHT,
      (transform) => {
        this.zoomTransform.set({ x: transform.x, y: transform.y, k: transform.k });
      }
    );
    this.measureContainer();

    // 3. Open the live-sync channel.
    const eventId = this.id() || this.store.venueData()?.venueId || 'STADIUM_ST_001';
    this.liveSync.connect(eventId, { simulate: !environment.production });

    // 3b. Fetch real event data so we have ticket types for the booking API.
    if (this.id()) {
      this.eventApp.getEvent(this.id()).pipe(takeUntil(this.destroy$)).subscribe((result) => {
        if (result.isSuccess && result.value) {
          this.eventData.set(result.value);
          const first = result.value.ticketTypes?.[0];
          if (first) {
            this.selectedTicketTypeId.set(first.id);
          }
        }
      });
    }

    // 4. Delta → surgical SVG update (skip Angular template eval).
    this.liveSync.deltas$
      .pipe(takeUntil(this.destroy$))
      .subscribe((delta) => {
        const seat = this.store.applyServerDelta(delta.seatId, delta.status);
        if (!seat) return;

        this.renderer.applyStatusDelta(delta.seatId, delta.status);
        this.lockOrchestrator.onServerDelta(delta.seatId, delta.status, seat.price);

        if (delta.status === 'RESERVED') {
          this.renderer.flashCollision(delta.seatId);
          this.pushToast({
            kind: 'warn',
            message: `Seat ${delta.seatId} was just reserved.`,
          });
        }
      });

    // 4b. Collision packets from server → flash + toast + rollback.
    this.liveSync.collisions$
      .pipe(takeUntil(this.destroy$))
      .subscribe((pkt) => {
        const seat = this.store.applyServerDelta(pkt.seatId, 'RESERVED');
        this.renderer.applyStatusDelta(pkt.seatId, 'RESERVED');
        this.renderer.flashCollision(pkt.seatId);
        this.pushToast({
          kind: 'error',
          message: `Seat ${pkt.seatId} is no longer available.`,
        });
      });

    // 4c. ACK packets → confirm lock acquired.
    this.liveSync.acks$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        // Lock confirmed — pendingLocks will be cleaned up on release
      });

    // 5. Lock orchestrator → flash + toast on every collision.
    this.lockOrchestrator.collisions$
      .pipe(takeUntil(this.destroy$))
      .subscribe((ev) => {
        this.renderer.flashCollision(ev.seatId);
        this.pushToast({
          kind: 'error',
          message: `Seat ${ev.seatId} is no longer available ($${ev.price.toLocaleString()}).`,
        });
      });
  }

  ngOnDestroy(): void {
    this.liveSync.disconnect();
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Event handlers ──

  setMode(mode: VenueMode): void {
    if (this.store.mode() === mode) return;
    // 1. Purge BEFORE switching so V8 can collect detached nodes.
    this.renderer.purgeGraph();
    // 2. Reset the in-memory store for the new geometric model.
    this.store.resetForModeSwitch();
    // 3. Flip the mode – the effect above will re-render the canvas.
    this.store.setMode(mode);
    this.pushToast({
      kind: 'info',
      message: `Switched to ${mode} mode (graph purged).`,
    });
  }

  private handleSeatClick(seat: SeatNode): void {
    if (this.submitting()) return;
    if (seat.status === 'RESERVED') return;
    const isSelected = this.store.toggleSeat(seat.id);
    if (!isSelected) {
      this.renderer.applyStatusDelta(seat.id, seat.status);
    } else {
      this.renderer.applyStatusDelta(seat.id, 'SELECTED');
    }
  }

  checkoutSelection(): void {
    void this.executeFinalBooking();
  }

  async executeFinalBooking(): Promise<void> {
    const seats = this.store.selectedSeatsMetadata();
    if (seats.length === 0) return;
    if (this.submitting()) return;

    const eventId = this.id() || this.store.venueData()?.venueId || '';
    const ticketTypeId = this.selectedTicketTypeId();
    if (!ticketTypeId) {
      this.pushToast({ kind: 'error', message: 'No ticket type available. Please try again later.' });
      return;
    }

    this.submitting.set(true);

    // Create booking via REST API.
    this.bookingApp.createBooking({
      eventId,
      ticketTypeId,
      quantity: seats.length,
      paymentMethod: this.paymentMethod(),
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: (result) => {
        this.submitting.set(false);
        if (result.isSuccess && result.value) {
          const { bookingId, paymentUrl } = result.value;
          if (this.paymentMethod() === 'Instant' && paymentUrl) {
            if (paymentUrl.startsWith('mock://')) {
              this.bookingApp.confirmBooking(bookingId).pipe(takeUntil(this.destroy$)).subscribe();
            } else {
              sessionStorage.setItem(`paymentUrl:${bookingId}`, paymentUrl);
              window.open(paymentUrl, '_blank');
            }
          }
          this.pushToast({
            kind: 'success',
            message: `Booking created! ${seats.length} seat${seats.length > 1 ? 's' : ''} reserved.`,
          });
          this.store.clearSelection();
          this.router.navigate(['/bookings', bookingId]);
        } else {
          this.pushToast({
            kind: 'error',
            message: result.errors?.[0]?.message ?? 'Booking failed.',
          });
        }
      },
      error: () => {
        this.submitting.set(false);
        this.pushToast({ kind: 'error', message: 'Booking request failed. Please try again.' });
      },
    });
  }

  handleBadgeHover(blockId: string): void {
    this.renderer.highlightBlock(blockId, true);
  }

  handleBadgeLeave(blockId: string): void {
    this.renderer.highlightBlock(blockId, false);
  }

  handleBadgeClick(blockId: string): void {
    // Optionally zoom to block or perform other actions
    console.info('[Eventy Engine] Badge clicked for block', blockId);
  }

  private handlePitClick(pit: StageFloorPit): void {
    console.info('[Eventy Engine] Floor pit clicked', pit);
  }

  private handleBlockClick(block: BlockGroup): void {
    console.info('[Eventy Engine] Block clicked', block.id);
  }
  private handleHover(event: MouseEvent, target: SeatNode | StageFloorPit | BlockGroup | null): void {
    const rect = this.svgRoot.nativeElement.getBoundingClientRect();
    this.tooltipX.set(event.clientX - rect.left);
    this.tooltipY.set(event.clientY - rect.top);

    if (!target) {
      this.tooltipPayload.set(null);
      this.tooltipKind.set('none');
      return;
    }

    if ('seats' in target) {
      this.tooltipPayload.set(null);
      this.tooltipKind.set('none');
      return;
    }

    this.tooltipPayload.set(target as SeatNode | StageFloorPit);
    this.tooltipKind.set('category' in target && !('rowLabel' in target) ? 'pit' : 'seat');
  }

  tooltipTransform(): string {
    if (this.tooltipKind() === 'none') {
      return 'translate(-9999px, -9999px)';
    }
    return `translate(${this.tooltipX() + 16}px, ${this.tooltipY() + 16}px)`;
  }

  badgeTransform(x: number, y: number): string {
    const scaleX = this.containerWidth() / VIEW_WIDTH || 1;
    const scaleY = this.containerHeight() / VIEW_HEIGHT || 1;
    const z = this.zoomTransform();
    // The point (x,y) inside the SVG maps to screen coordinates using zoom transform first, then container scale
    const zoomedX = x * z.k + z.x;
    const zoomedY = y * z.k + z.y;
    return `translate(${zoomedX * scaleX}px, ${zoomedY * scaleY}px)`;
  }

  clearSelection(): void {
    const ids = Array.from(this.store.selectedSeatIds());
    this.store.clearSelection();
    const data = this.store.venueData();
    if (!data) return;
    data.zones.forEach((z) =>
      z.blocks.forEach((b) =>
        b.seats.forEach((s) => {
          if (ids.includes(s.id)) {
            this.renderer.applyStatusDelta(s.id, s.status);
          }
        }),
      ),
    );
  }

  flattenVenue(): void {
    const data = this.store.venueData();
    if (!data) return;
    const payload = this.transformer.flattenVenueToBackend(data);
    console.info('[Eventy Engine] Flattened venue payload →', payload);
    this.pushToast({ kind: 'success', message: 'Layout flattened and ready for backend POST.' });
  }

  resetZoom(): void {
    this.renderer.resetZoom();
  }

  // ── Toasts ──

  private pushToast(t: Omit<ToastItem, 'id'>): void {
    const id = ++this.toastId;
    this.toasts.update((arr) => [...arr, { id, ...t }]);
    setTimeout(() => this.dismissToast(id), 4500);
  }

  dismissToast(id: number): void {
    this.toasts.update((arr) => arr.filter((t) => t.id !== id));
  }

  // ── Internal ──

  @HostListener('window:resize')
  onResize(): void {
    this.measureContainer();
  }

  private measureContainer(): void {
    if (!this.svgRoot) return;
    const rect = this.svgRoot.nativeElement.getBoundingClientRect();
    this.containerWidth.set(rect.width);
    this.containerHeight.set(rect.height);
  }
}
