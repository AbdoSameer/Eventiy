import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DecimalPipe } from '@angular/common';

export interface PriceBadgeModel {
  blockId: string;
  blockName: string;
  price: number;
  x: number;
  y: number;
  status: 'AVAILABLE' | 'FAST_SELLING' | 'RESERVED' | 'SELECTED';
}

/**
 * Floating financial metric pill that hovers over a venue block.
 * Pure presentation – the parent positions it via CSS transform.
 */
@Component({
  selector: 'app-price-badge',
  standalone: true,
  imports: [CommonModule, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="price-badge"
      [class.status-available]="status() === 'AVAILABLE'"
      [class.status-fast]="status() === 'FAST_SELLING'"
      [style.transform]="positionTransform()"
      (mouseenter)="hover.emit()"
      (mouseleave)="leave.emit()"
      (click)="click.emit()"
    >
      <span class="price-dot"></span>
      <span class="price-amount">\${{ price() | number: '1.0-0' }}</span>
    </button>
  `,
  styles: [
    `
      :host {
        position: absolute;
        inset: 0;
        pointer-events: none;
        z-index: 10;
      }
      .price-badge {
        position: absolute;
        top: 0;
        left: 0;
        display: inline-flex;
        align-items: center;
        gap: 0.4rem;
        padding: 0.35rem 0.7rem 0.35rem 0.5rem;
        border-radius: 9999px;
        font-size: 0.75rem;
        font-weight: 700;
        color: #1f2937;
        background: #ffffff;
        border: 1px solid #e5e7eb;
        box-shadow: 0 4px 10px -2px rgba(0, 0, 0, 0.12);
        will-change: transform;
        transition: transform 80ms linear, opacity 200ms ease, box-shadow 200ms ease;
        pointer-events: auto;
        cursor: pointer;
      }
      .price-badge:hover {
        box-shadow: 0 6px 14px -2px rgba(0, 0, 0, 0.2);
        transform: scale(1.05);
        z-index: 20;
      }
      .price-dot {
        width: 0.55rem;
        height: 0.55rem;
        border-radius: 9999px;
        background: #9ca3af;
        box-shadow: 0 0 0 2px rgba(156, 163, 175, 0.15);
      }
      .status-available .price-dot {
        background: #10b981;
        box-shadow: 0 0 0 2px rgba(16, 185, 129, 0.15);
      }
      .status-fast .price-dot {
        background: #f6544c;
        box-shadow: 0 0 0 2px rgba(246, 84, 76, 0.15);
      }
    `,
  ],
})
export class PriceBadgeComponent {
  readonly price = input.required<number>();
  readonly positionTransform = input.required<string>();
  readonly status = input<'AVAILABLE' | 'FAST_SELLING' | 'RESERVED' | 'SELECTED'>('AVAILABLE');
  readonly hover = output<void>();
  readonly leave = output<void>();
  readonly click = output<void>();
}
