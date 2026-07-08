import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SkeletonType = 'card' | 'list' | 'detail';

/**
 * Pulse-animated skeleton used while data loads (no spinners).
 *
 * - `card`   → grid of horizontal placeholders matching EventCardComponent
 * - `list`   → stacked rows for tables / dashboard lists
 * - `detail` → large hero + body blocks for the detail page
 */
@Component({
  selector: 'app-skeleton-loader',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @switch (type) {
      @case ('card') {
        <div class="skel-grid">
          @for (i of placeholders(6); track i) {
            <div class="skel-card animate-pulse">
              <div class="skel-card-image"></div>
              <div class="skel-card-body">
                <div class="skel-line skel-line-lg"></div>
                <div class="skel-line skel-line-sm"></div>
                <div class="skel-line skel-line-sm"></div>
                <div class="skel-card-footer">
                  <div class="skel-avatars">
                    <div class="skel-avatar"></div>
                    <div class="skel-avatar"></div>
                    <div class="skel-avatar"></div>
                  </div>
                  <div class="skel-badge"></div>
                </div>
              </div>
            </div>
          }
        </div>
      }
      @case ('list') {
        <div class="skel-list">
          @for (i of placeholders(5); track i) {
            <div class="skel-list-row animate-pulse">
              <div class="skel-list-circle"></div>
              <div class="skel-list-text">
                <div class="skel-line skel-line-sm"></div>
                <div class="skel-line skel-line-xs"></div>
              </div>
              <div class="skel-line skel-line-btn"></div>
            </div>
          }
        </div>
      }
      @case ('detail') {
        <div class="skel-detail animate-pulse">
          <div class="skel-detail-hero"></div>
          <div class="skel-detail-content">
            <div class="skel-line skel-line-xl"></div>
            <div class="skel-line skel-line-sm"></div>
            <div class="skel-line skel-line-block"></div>
            <div class="skel-line skel-line-block"></div>
          </div>
        </div>
      }
    }
  `,
  styles: [`
    /* ── Card Skeleton (Horizontal) ─────────────────── */
    .skel-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
      gap: 1.5rem;
    }

    .skel-card {
      display: flex;
      background: #ffffff;
      border-radius: 1rem;
      border: 1px solid #E5E7EB;
      overflow: hidden;
    }

    .skel-card-image {
      width: 200px;
      flex-shrink: 0;
      background: #E5E7EB;
    }

    .skel-card-body {
      flex: 1;
      padding: 1.25rem;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .skel-card-footer {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-top: auto;
      padding-top: 0.75rem;
    }

    .skel-avatars {
      display: flex;
      gap: 0.375rem;
    }

    .skel-avatar {
      width: 1.75rem;
      height: 1.75rem;
      border-radius: 9999px;
      background: #E5E7EB;
    }

    .skel-badge {
      height: 1.75rem;
      width: 3.5rem;
      border-radius: 9999px;
      background: #E5E7EB;
    }

    /* ── Shared lines ────────────────────────────────── */
    .skel-line {
      background: #E5E7EB;
      border-radius: 0.25rem;
    }

    .skel-line-xs { height: 0.75rem; width: 25%; }
    .skel-line-sm { height: 0.875rem; width: 50%; }
    .skel-line-lg { height: 1.125rem; width: 75%; }
    .skel-line-xl { height: 2rem; width: 66%; }
    .skel-line-block { height: 6rem; width: 100%; }
    .skel-line-btn { height: 2rem; width: 5rem; border-radius: 9999px; }

    /* ── List Skeleton ───────────────────────────────── */
    .skel-list {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .skel-list-row {
      background: #ffffff;
      border-radius: 1rem;
      border: 1px solid #E5E7EB;
      padding: 1.25rem;
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .skel-list-circle {
      width: 3rem;
      height: 3rem;
      border-radius: 9999px;
      background: #E5E7EB;
      flex-shrink: 0;
    }

    .skel-list-text {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    /* ── Detail Skeleton ─────────────────────────────── */
    .skel-detail {
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }

    .skel-detail-hero {
      width: 100%;
      height: 24rem;
      background: #E5E7EB;
      border-radius: 1rem;
    }

    .skel-detail-content {
      max-width: 56rem;
      margin: 0 auto;
      padding: 0 1rem;
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    /* ── Responsive ──────────────────────────────────── */
    @media (max-width: 640px) {
      .skel-grid {
        grid-template-columns: 1fr;
      }

      .skel-card {
        flex-direction: column;
      }

      .skel-card-image {
        width: 100%;
        height: 180px;
      }
    }
  `],
})
export class SkeletonLoaderComponent {
  @Input() type: SkeletonType = 'card';

  /** Helper to iterate N times in the template. */
  protected placeholders(count: number): number[] {
    return Array.from({ length: count }, (_, i) => i);
  }
}
