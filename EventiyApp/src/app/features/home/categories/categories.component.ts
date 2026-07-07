import { ChangeDetectionStrategy, Component, EventEmitter, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CATEGORY_META, EVENT_CATEGORIES } from '../../../core/models/event.model';

/**
 * Horizontally scrollable row of category chips.
 *
 * Emits the selected category name (or empty string to clear the filter).
 * The active chip is highlighted in coral.
 */
@Component({
  selector: 'app-categories',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      <h2 class="text-2xl font-bold text-text-primary mb-5">Browse by category</h2>

      <div class="flex gap-3 overflow-x-auto pb-3 -mx-1 px-1">
        <button
          (click)="select('')"
          class="rounded-full px-4 py-2 font-medium whitespace-nowrap transition-colors"
          [class]="activeCategory() === '' ? activeClass : inactiveClass"
        >
          ✨ All
        </button>

        @for (category of categories; track category) {
          <button
            (click)="select(category)"
            class="rounded-full px-4 py-2 font-medium whitespace-nowrap transition-colors"
            [class]="activeCategory() === category ? activeClass : inactiveClass"
          >
            {{ meta[category].emoji }} {{ meta[category].label }}
          </button>
        }
      </div>
    </section>
  `,
})
export class CategoriesComponent {
  @Output() categorySelect = new EventEmitter<string>();

  protected readonly categories = EVENT_CATEGORIES;
  protected readonly meta = CATEGORY_META;

  /** Currently active category ('' = no filter). */
  readonly activeCategory = signal('');

  protected readonly activeClass = 'bg-primary text-white';
  protected readonly inactiveClass = 'bg-gray-100 text-text-primary hover:bg-primary hover:text-white';

  select(category: string): void {
    this.activeCategory.set(category);
    this.categorySelect.emit(category);
  }
}
