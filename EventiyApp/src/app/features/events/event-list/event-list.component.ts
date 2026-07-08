import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EventsGridComponent } from '../../../features/home/events-grid/events-grid.component';
import { EventApplicationService } from '../../../application/services/event-application.service';
import { ToastService } from '../../../infrastructure/services/toast.service';
import { Event, EVENT_CATEGORIES } from '../../../core/models/event.model';

type SortKey = 'date' | 'price-asc' | 'price-desc' | 'popularity';

@Component({
  selector: 'app-event-list',
  standalone: true,
  imports: [CommonModule, FormsModule, EventsGridComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-background-alt min-h-screen">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
        <header class="mb-8">
          <h1 class="text-3xl font-bold text-text-primary">Browse events</h1>
          <p class="text-text-secondary mt-1">Find something to do tonight or this weekend.</p>
        </header>

        <div class="grid grid-cols-1 lg:grid-cols-[260px_1fr] gap-8">
          <!-- Sidebar filters -->
          <aside class="bg-white rounded-2xl shadow-md p-6 h-fit lg:sticky lg:top-24">
            <h2 class="text-lg font-semibold text-text-primary mb-4">Filters</h2>

            <!-- Nearby toggle -->
            <div class="mb-6">
              <button
                (click)="toggleNearby()"
                [class.bg-primary]="eventApp.nearbyEnabled()"
                [class.text-white]="eventApp.nearbyEnabled()"
                [class.border-primary]="eventApp.nearbyEnabled()"
                class="w-full rounded-full border-2 border-gray-300 px-4 py-2 font-semibold hover:bg-primary hover:text-white hover:border-primary transition-colors flex items-center justify-center gap-2"
              >
                <span>{{ eventApp.nearbyEnabled() ? '📍' : '🌍' }}</span>
                {{ eventApp.nearbyEnabled() ? 'Nearby ON' : 'Show Nearby' }}
              </button>
            </div>

            <!-- Category -->
            <div class="mb-6">
              <label class="block text-sm font-semibold text-text-primary mb-2">Category</label>
              <select
                [(ngModel)]="selectedCategory"
                (ngModelChange)="onFilterChange()"
                class="w-full rounded-lg border border-gray-300 px-3 py-2 text-text-primary focus:border-primary focus:ring-primary"
              >
                <option value="">All categories</option>
                @for (category of categories; track category) {
                  <option [value]="category">{{ category }}</option>
                }
              </select>
            </div>

            <!-- Date -->
            <div class="mb-6">
              <label class="block text-sm font-semibold text-text-primary mb-2">Date</label>
              <select
                [(ngModel)]="selectedDate"
                (ngModelChange)="recompute()"
                class="w-full rounded-lg border border-gray-300 px-3 py-2 text-text-primary focus:border-primary focus:ring-primary"
              >
                <option value="">Any date</option>
                <option value="today">Today</option>
                <option value="tomorrow">Tomorrow</option>
                <option value="week">This week</option>
                <option value="month">This month</option>
              </select>
            </div>

            <!-- Price range -->
            <div class="mb-6">
              <label class="block text-sm font-semibold text-text-primary mb-2">
                Max price: <span class="text-primary">{{ '$' + maxPrice }}</span>
              </label>
              <input
                type="range"
                min="0"
                max="500"
                step="10"
                [(ngModel)]="maxPrice"
                (ngModelChange)="recompute()"
                class="w-full accent-[#F6544C]"
              />
            </div>

            <button
              (click)="resetFilters()"
              class="w-full rounded-full border-2 border-primary text-primary px-4 py-2 font-semibold hover:bg-primary hover:text-white transition-colors"
            >
              Reset filters
            </button>
          </aside>

          <!-- Results -->
          <section>
            <div class="flex items-center justify-between mb-6">
              <p class="text-text-secondary">
                <span class="font-semibold text-text-primary">{{ visibleEvents().length }}</span> events found
              </p>
              <div class="flex items-center gap-2">
                <label for="sort" class="text-sm text-text-secondary">Sort by:</label>
                <select
                  id="sort"
                  [(ngModel)]="sortBy"
                  (ngModelChange)="recompute()"
                  class="rounded-lg border border-gray-300 px-3 py-2 text-text-primary focus:border-primary focus:ring-primary"
                >
                  <option value="date">Date</option>
                  <option value="price-asc">Price: Low to High</option>
                  <option value="price-desc">Price: High to Low</option>
                  <option value="popularity">Popularity</option>
                </select>
              </div>
            </div>

            <app-events-grid [events]="visibleEvents()" [loading]="loading()" />
          </section>
        </div>
      </div>
    </div>
  `,
})
export class EventListComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  protected eventApp = inject(EventApplicationService);
  private toast = inject(ToastService);

  protected readonly categories = EVENT_CATEGORIES;

  private readonly allEvents = signal<Event[]>([]);
  readonly visibleEvents = signal<Event[]>([]);
  readonly loading = signal(false);

  // Filter state (bound with ngModel)
  selectedCategory = '';
  selectedDate = '';
  maxPrice = 500;
  sortBy: SortKey = 'date';

  ngOnInit(): void {
    this.loadEvents();
  }

  toggleNearby(): void {
    this.eventApp.toggleNearby();
    this.loadEvents();
  }

  onFilterChange(): void {
    this.loadEvents();
  }

  private loadEvents(): void {
    this.loading.set(true);
    const query = this.selectedCategory
      ? { type: this.selectedCategory }
      : undefined;
    this.eventApp.getEvents(true, query).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        if (result.isSuccess && result.value) {
          this.allEvents.set(result.value);
        } else {
          this.toast.showError(result.errors?.[0]?.message ?? 'Failed to load events.');
        }
        this.loading.set(false);
        this.recompute();
      },
      error: () => {
        this.toast.showError('Could not reach the server.');
        this.loading.set(false);
      },
    });
  }

  /** Recompute the visible list from current filters + sort. */
  recompute(): void {
    let items = this.allEvents().filter((e) => {
      const matchesCategory = !this.selectedCategory || e.type === this.selectedCategory;
      const matchesPrice = (e.price ?? 0) <= this.maxPrice;
      const matchesDate = this.matchesDate(e.date);
      return matchesCategory && matchesPrice && matchesDate;
    });

    items = items.sort((a, b) => {
      switch (this.sortBy) {
        case 'price-asc':
          return (a.price ?? 0) - (b.price ?? 0);
        case 'price-desc':
          return (b.price ?? 0) - (a.price ?? 0);
        case 'popularity':
          return (b.attendeeCount ?? 0) - (a.attendeeCount ?? 0);
        case 'date':
        default:
          return new Date(a.date).getTime() - new Date(b.date).getTime();
      }
    });

    this.visibleEvents.set(items);
  }

  private matchesDate(dateStr: string): boolean {
    if (!this.selectedDate) {
      return true;
    }
    const date = new Date(dateStr);
    const now = new Date();
    const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate());
    const eventDay = startOfDay(date).getTime();
    const today = startOfDay(now).getTime();
    const dayMs = 24 * 60 * 60 * 1000;

    switch (this.selectedDate) {
      case 'today':
        return eventDay === today;
      case 'tomorrow':
        return eventDay === today + dayMs;
      case 'week':
        return eventDay >= today && eventDay <= today + 7 * dayMs;
      case 'month':
        return date.getMonth() === now.getMonth() && date.getFullYear() === now.getFullYear();
      default:
        return true;
    }
  }

  resetFilters(): void {
    this.selectedCategory = '';
    this.selectedDate = '';
    this.maxPrice = 500;
    this.sortBy = 'date';
    this.loadEvents();
  }
}
