import { Injectable, inject, signal } from '@angular/core';
import { Subject, Observable } from 'rxjs';
import { AuthApplicationService } from '../../../../../application/services/auth-application.service';
import { environment } from '../../../../../../environments/environment';

export interface SeatStateDelta {
  seatId: string;
  status: 'AVAILABLE' | 'RESERVED' | 'FAST_SELLING';
  ts?: number;
}

export interface CollisionPacket {
  seatId: string;
  reason: string;
  ts?: number;
}

export type SyncStatus = 'IDLE' | 'CONNECTING' | 'CONNECTED' | 'RECONNECTING' | 'ERROR';

@Injectable({ providedIn: 'root' })
export class LiveStateSyncService {
  private readonly auth = inject(AuthApplicationService);

  private readonly deltaSubject = new Subject<SeatStateDelta>();
  readonly deltas$: Observable<SeatStateDelta> = this.deltaSubject.asObservable();

  private readonly collisionSubject = new Subject<CollisionPacket>();
  readonly collisions$: Observable<CollisionPacket> = this.collisionSubject.asObservable();

  private readonly ackSubject = new Subject<string>();
  readonly acks$: Observable<string> = this.ackSubject.asObservable();

  readonly status = signal<SyncStatus>('IDLE');
  readonly lastDeltaAt = signal<number | null>(null);
  readonly reconnectAttempts = signal(0);
  readonly connectionId = signal<string | null>(null);

  private socket: WebSocket | null = null;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private heartbeatTimer: ReturnType<typeof setInterval> | null = null;
  private venueId: string | null = null;
  private manualDisconnect = false;

  public connect(venueId: string, opts: { simulate?: boolean } = { simulate: false }): void {
    this.disconnect();
    this.venueId = venueId;
    this.manualDisconnect = false;

    if (opts.simulate) {
      this.status.set('CONNECTED');
      return;
    }

    this.status.set('CONNECTING');

    const token = this.auth.getToken();
    const wsBase = environment.wsUrl ?? `wss://${location.host}`;
    const url = `${wsBase}/ws/venues/${venueId}?token=${encodeURIComponent(token ?? '')}`;

    try {
      this.socket = new WebSocket(url);
    } catch {
      this.status.set('ERROR');
      this.scheduleReconnect();
      return;
    }

    this.socket.onopen = () => {
      this.status.set('CONNECTED');
      this.reconnectAttempts.set(0);
      this.startHeartbeat();
    };

    this.socket.onmessage = (e) => this.handleMessage(e.data);

    this.socket.onerror = () => {
      this.status.set('ERROR');
    };

    this.socket.onclose = () => {
      this.stopHeartbeat();
      if (!this.manualDisconnect) {
        this.scheduleReconnect();
      }
    };
  }

  public disconnect(): void {
    this.manualDisconnect = true;
    this.stopHeartbeat();
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
    if (this.socket) {
      this.socket.close(1000, 'Client disconnect');
      this.socket = null;
    }
    this.status.set('IDLE');
    this.connectionId.set(null);
  }

  public send(raw: string): void {
    if (this.socket?.readyState === WebSocket.OPEN) {
      this.socket.send(raw);
    }
  }

  public emit(delta: SeatStateDelta): void {
    this.lastDeltaAt.set(Date.now());
    this.deltaSubject.next(delta);
  }

  private handleMessage(raw: string): void {
    try {
      const msg = JSON.parse(raw);
      switch (msg.type) {
        case 'DELTA':
          this.lastDeltaAt.set(Date.now());
          this.deltaSubject.next({
            seatId: msg.seatId,
            status: msg.status,
            ts: msg.ts ?? Date.now(),
          });
          break;

        case 'COLLISION':
          this.collisionSubject.next({
            seatId: msg.seatId,
            reason: msg.status,
            ts: msg.ts,
          });
          break;

        case 'PING':
          this.send(JSON.stringify({ type: 'PONG' }));
          break;

        case 'CONNECTED':
          this.connectionId.set(msg.connectionId);
          break;

        case 'ACK':
          this.ackSubject.next(msg.seatId);
          break;
      }
    } catch {
      // malformed – drop
    }
  }

  private startHeartbeat(): void {
    this.heartbeatTimer = setInterval(() => {
      if (this.socket?.readyState === WebSocket.OPEN) {
        this.send(JSON.stringify({ type: 'PING' }));
      }
    }, 25_000);
  }

  private stopHeartbeat(): void {
    if (this.heartbeatTimer) {
      clearInterval(this.heartbeatTimer);
      this.heartbeatTimer = null;
    }
  }

  private scheduleReconnect(): void {
    if (this.manualDisconnect || !this.venueId) return;
    this.status.set('RECONNECTING');
    const attempt = this.reconnectAttempts() + 1;
    this.reconnectAttempts.set(attempt);
    const delay = Math.min(1000 * 2 ** (attempt - 1), 30_000);
    this.reconnectTimer = setTimeout(() => this.connect(this.venueId!, { simulate: false }), delay);
  }
}
