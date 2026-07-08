import { ChangeDetectionStrategy, Component, EventEmitter, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CATEGORY_META, EVENT_CATEGORIES } from '../../../core/models/event.model';

/**
 * Meetup-inspired category section with scrollable card row.
 *
 * Each category is a compact card with emoji, label, and optional event count.
 * Emits the selected category name (or empty string to clear the filter).
 */
@Component({
  selector: 'app-categories',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="categories-section">
      <div class="categories-header">
        <h2 class="categories-title">Explore by category</h2>
        <button class="categories-view-all" (click)="select('')">
          See all →
        </button>
      </div>

      <div class="categories-scroll">
        <!-- "All" card -->
        <button
          (click)="select('')"
          class="category-card"
          [class.active]="activeCategory() === ''"
        >
          <svg class="category-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 13.5V3.75m0 9.75a1.5 1.5 0 010 3m0-3a1.5 1.5 0 000 3m0 3.75V16.5m0-12.75V3.75m0 9.75V3.75m9 9V3.75m0 9.75a1.5 1.5 0 010 3m0-3a1.5 1.5 0 000 3m0 3.75V16.5m0-12.75V3.75m0 9.75V3.75M9 6.75h6M9 9.75h6M9 12.75h6" />
          </svg>
          <span class="category-label">All</span>
        </button>

        @for (category of categories; track category) {
          <button
            (click)="select(category)"
            class="category-card"
            [class.active]="activeCategory() === category"
          >
            <svg class="category-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" [attr.d]="meta[category].iconPath" />
            </svg>
            <span class="category-label">{{ meta[category].label }}</span>
          </button>
        }
      </div>
    </section>
  `,
  styles: [`
    /* ── Categories Section ─────────────────────────── */
    .categories-section {
      max-width: 1280px;
      margin: 0 auto;
      padding: 3rem 2rem 1.5rem;
    }

    .categories-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 1.5rem;
    }

    .categories-title {
      font-size: 1.5rem;
      font-weight: 700;
      color: #1F2937;
      margin: 0;
    }

    .categories-view-all {
      background: none;
      border: none;
      color: #F6544C;
      font-size: 0.9375rem;
      font-weight: 600;
      cursor: pointer;
      padding: 0.5rem 0;
      font-family: inherit;
      transition: opacity 0.2s;
    }

    .categories-view-all:hover {
      opacity: 0.75;
    }

    /* ── Scrollable Card Row ────────────────────────── */
    .categories-scroll {
      display: flex;
      gap: 0.875rem;
      overflow-x: auto;
      padding-bottom: 1rem;
      scroll-snap-type: x mandatory;
      -webkit-overflow-scrolling: touch;
      scrollbar-width: none;
    }

    .categories-scroll::-webkit-scrollbar {
      display: none;
    }

    /* ── Category Card ───────────────────────────────── */
    .category-card {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.625rem;
      padding: 1.25rem 1.5rem;
      min-width: 110px;
      border-radius: 1rem;
      border: 2px solid #E5E7EB;
      background: #ffffff;
      cursor: pointer;
      transition: all 0.2s ease;
      scroll-snap-align: start;
      flex-shrink: 0;
      font-family: inherit;
    }

    .category-card:hover {
      border-color: #F6544C;
      transform: translateY(-2px);
      box-shadow: 0 4px 12px rgba(246, 84, 76, 0.15);
    }

    .category-card.active {
      background: #F6544C;
      border-color: #F6544C;
      color: #ffffff;
      box-shadow: 0 4px 12px rgba(246, 84, 76, 0.3);
    }

    .category-card.active:hover {
      background: #E53E3E;
    }

    .category-icon {
      width: 1.75rem;
      height: 1.75rem;
      color: inherit;
    }

    .active .category-icon {
      color: #ffffff;
    }

    .category-card .category-icon {
      transition: color 0.2s;
    }

    .category-label {
      font-size: 0.875rem;
      font-weight: 600;
      color: inherit;
      white-space: nowrap;
    }

    /* ── Responsive ──────────────────────────────────── */
    @media (max-width: 640px) {
      .categories-section {
        padding: 2rem 1rem 1rem;
      }

      .category-card {
        min-width: 95px;
        padding: 1rem 1.125rem;
      }
    }
  `],
})
export class CategoriesComponent {
  @Output() categorySelect = new EventEmitter<string>();

  protected readonly categories = EVENT_CATEGORIES;
  protected readonly meta = CATEGORY_META;

  /** Currently active category ('' = no filter). */
  readonly activeCategory = signal('');

  select(category: string): void {
    this.activeCategory.set(category);
    this.categorySelect.emit(category);
  }
}
