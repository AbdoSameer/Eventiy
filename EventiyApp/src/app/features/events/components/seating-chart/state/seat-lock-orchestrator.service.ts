import { Injectable, inject, signal } from '@angular/core';
import { Observable, Subject } from 'rxjs';
import { VenueViewStore } from '../state/venue-view.store';
import { SeatStatus } from '../models/venue-graph.interfaces';
import { LiveStateSyncService } from '../renderer/live-state-sync.service';

export interface SeatCollisionEvent {
  seatId: string;
  reason: 'RESERVED_BY_OTHER' | 'STALE_LOCK';
  price: number;
}

/**
 * Optimistic-locking orchestrator.
 *
 * Wires user clicks to the WebSocket LOCK/UNLOCK protocol:
 *   Phase 1 (user click):
 *     1. Mark seat as SELECTED in the Signal array instantly.
 *     2. Send { type: "LOCK", seatId, ticketTypeId, eventId } over WebSocket.
 *
 *   Phase 2 (server sends ACK → lock confirmed):
 *     - Remove from pendingLocks.
 *
 *   Phase 2 (server sends COLLISION → lock rejected):
 *     - Rollback the optimistic selection.
 *     - Fire SeatCollisionEvent so the orchestrator flashes the node + shows a toast.
 *
 *   Phase 2 (server broadcasts DELTA with RESERVED for a seat in our cart):
 *     - Same rollback as COLLISION.
 */
@Injectable({ providedIn: 'root' })
export class SeatLockOrchestratorService {
  private readonly store = inject(VenueViewStore);
  private readonly sync = inject(LiveStateSyncService);

  private readonly _pendingLocks = signal<Set<string>>(new Set());
  readonly pendingLocks = this._pendingLocks.asReadonly();

  private readonly collisionSubject = new Subject<SeatCollisionEvent>();
  readonly collisions$: Observable<SeatCollisionEvent> = this.collisionSubject.asObservable();

  private readonly acquiredSubject = new Subject<string>();
  readonly acquired$: Observable<string> = this.acquiredSubject.asObservable();

  private ticketTypeId = '';
  private eventId = '';

  public setContext(eventId: string, ticketTypeId: string): void {
    this.eventId = eventId;
    this.ticketTypeId = ticketTypeId;
  }

  public async acquire(seatId: string, price: number): Promise<boolean> {
    if (this.store.isSelected(seatId)) return true;
    this.store.toggleSeat(seatId);
    this._pendingLocks.update((s) => new Set(s).add(seatId));

    this.sync.send(JSON.stringify({
      type: 'LOCK',
      seatId,
      ticketTypeId: this.ticketTypeId,
      eventId: this.eventId,
    }));

    return true;
  }

  public release(seatId: string): void {
    if (!this.store.isSelected(seatId)) return;
    this.store.toggleSeat(seatId);
    this._pendingLocks.update((s) => {
      const next = new Set(s);
      next.delete(seatId);
      return next;
    });

    this.sync.send(JSON.stringify({
      type: 'UNLOCK',
      seatId,
      eventId: this.eventId,
    }));
  }

  public onServerDelta(seatId: string, status: SeatStatus, price = 0): void {
    if (status !== 'RESERVED') return;
    if (!this.store.isSelected(seatId)) return;

    this.store.toggleSeat(seatId);
    this._pendingLocks.update((s) => {
      const next = new Set(s);
      next.delete(seatId);
      return next;
    });

    this.collisionSubject.next({
      seatId,
      reason: 'RESERVED_BY_OTHER',
      price,
    });
  }
}
