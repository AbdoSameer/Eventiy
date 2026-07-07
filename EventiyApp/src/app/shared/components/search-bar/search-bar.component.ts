import { ChangeDetectionStrategy, Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface SearchCriteria {
  keyword: string;
  location: string;
}

/**
 * Dual-input search bar embedded in the hero.
 *
 * Emits a `search` event with the keyword + location so the parent
 * (HomeComponent) can filter the events list — no service calls here.
 */
@Component({
  selector: 'app-search-bar',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form
      (ngSubmit)="onSubmit()"
      class="bg-white rounded-2xl shadow-lg border border-border p-2 flex flex-col sm:flex-row gap-2"
    >
      <!-- Keyword -->
      <div class="flex-1 flex items-center gap-2 px-3">
        <svg class="w-5 h-5 text-text-secondary shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
        <input
          type="text"
          name="keyword"
          [(ngModel)]="keyword"
          placeholder="What do you want to do?"
          class="w-full py-3 text-text-primary placeholder:text-text-secondary bg-transparent border-0 focus:ring-0"
          aria-label="Search by keyword"
        />
      </div>

      <div class="hidden sm:block w-px bg-border"></div>

      <!-- Location -->
      <div class="flex-1 flex items-center gap-2 px-3">
        <svg class="w-5 h-5 text-text-secondary shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
        </svg>
        <input
          type="text"
          name="location"
          [(ngModel)]="location"
          placeholder="Location"
          class="w-full py-3 text-text-primary placeholder:text-text-secondary bg-transparent border-0 focus:ring-0"
          aria-label="Search by location"
        />
      </div>

      <button
        type="submit"
        class="rounded-xl bg-primary text-white px-6 py-3 font-semibold hover:bg-primary-dark transition-colors flex items-center justify-center gap-2"
      >
        <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
        <span>Search</span>
      </button>
    </form>
  `,
})
export class SearchBarComponent {
  @Output() search = new EventEmitter<SearchCriteria>();

  keyword = '';
  location = '';

  onSubmit(): void {
    this.search.emit({
      keyword: this.keyword.trim(),
      location: this.location.trim(),
    });
  }
}
