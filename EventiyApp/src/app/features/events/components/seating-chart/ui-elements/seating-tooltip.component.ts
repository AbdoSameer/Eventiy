import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { SeatNode, StageFloorPit } from '../models/venue-graph.interfaces';

export type TooltipPayload = SeatNode | StageFloorPit | null;

/**
 * Detached HTML overlay that follows the cursor.
 *
 * Receives a payload via input() and is repositioned by the
 * parent (the orchestrator) using mouse coordinates – zero
 * frame latency because the element is moved via transform.
 */
@Component({
  selector: 'app-seating-tooltip',
  standalone: true,
  imports: [CommonModule, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (payload(); as p) {
      <div
        class="tooltip-card"
        [style.transform]="positionTransform()"
        role="tooltip"
      >
        @if (kind() === 'seat') {
          <div class="tooltip-row">
            <span class="tooltip-label">Ticket Type</span>
            <span class="tooltip-value">{{ asSeat(p).category }}</span>
          </div>
          <div class="tooltip-row">
            <span class="tooltip-label">Section</span>
            <span class="tooltip-value">{{ asSeat(p).id.split('_')[1] || asSeat(p).id }}</span>
          </div>
          <div class="tooltip-row">
            <span class="tooltip-label">Price</span>
            <span class="tooltip-value tooltip-price">\${{ asSeat(p).price | number: '1.0-0' }}</span>
          </div>
          <div class="tooltip-row">
            <span class="tooltip-label">Status</span>
            <span class="tooltip-badge" [class]="'status-' + asSeat(p).status.toLowerCase()">
              {{ statusLabel(asSeat(p).status) }}
            </span>
          </div>
        } @else if (kind() === 'pit') {
          <div class="tooltip-row">
            <span class="tooltip-label">Ticket Type</span>
            <span class="tooltip-value">{{ asPit(p).name }}</span>
          </div>
          <div class="tooltip-row">
            <span class="tooltip-label">Section</span>
            <span class="tooltip-value">{{ asPit(p).id }}</span>
          </div>
          <div class="tooltip-row">
            <span class="tooltip-label">Price</span>
            <span class="tooltip-value tooltip-price">\${{ asPit(p).price | number: '1.0-0' }}</span>
          </div>
          <div class="tooltip-row">
            <span class="tooltip-label">Status</span>
            <span class="tooltip-badge" [class]="'status-' + asPit(p).status.toLowerCase()">
              {{ statusLabel(asPit(p).status) }}
            </span>
          </div>
        }
      </div>
    }
  `,
  styles: [
    `
      :host {
        position: absolute;
        inset: 0;
        pointer-events: none;
        z-index: 50;
      }
      .tooltip-card {
        position: absolute;
        top: 0;
        left: 0;
        background: #ffffff;
        color: #1f2937;
        padding: 0.75rem 1rem;
        border-radius: 0.75rem;
        box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.15),
          0 8px 10px -6px rgba(0, 0, 0, 0.1);
        border: 1px solid #e5e7eb;
        min-width: 240px;
        font-size: 0.85rem;
        will-change: transform;
        transition: transform 60ms linear;
      }
      .tooltip-row {
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 0.75rem;
        padding: 0.15rem 0;
      }
      .tooltip-label {
        color: #6b7280;
        font-weight: 500;
      }
      .tooltip-value {
        color: #1f2937;
        font-weight: 600;
        text-align: right;
      }
      .tooltip-price {
        color: #f6544c;
        font-size: 1rem;
      }
      .tooltip-badge {
        padding: 0.1rem 0.5rem;
        border-radius: 9999px;
        font-size: 0.7rem;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.05em;
      }
      .status-available {
        background: #d1fae5;
        color: #047857;
      }
      .status-reserved {
        background: #fee2e2;
        color: #b91c1c;
      }
      .status-fast_selling {
        background: #fef3c7;
        color: #b45309;
      }
      .status-selected {
        background: #ddd6fe;
        color: #5b21b6;
      }
    `,
  ],
})
export class SeatingTooltipComponent {
  readonly payload = input<TooltipPayload>(null);
  readonly kind = input<'seat' | 'pit' | 'none'>('none');
  /** CSS transform string, e.g. "translate(120px, 200px)". */
  readonly positionTransform = input<string>('translate(-9999px, -9999px)');

  asSeat(p: TooltipPayload): SeatNode {
    return p as SeatNode;
  }
  asPit(p: TooltipPayload): StageFloorPit {
    return p as StageFloorPit;
  }
  statusLabel(s: string): string {
    return s.replace(/_/g, ' ').toLowerCase();
  }
}
