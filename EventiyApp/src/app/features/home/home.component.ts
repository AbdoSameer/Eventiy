import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HeroComponent } from './hero/hero.component';
import { HowItWorksComponent } from './how-it-works/how-it-works.component';
import { CategoriesComponent } from './categories/categories.component';
import { EventsGridComponent } from './events-grid/events-grid.component';
import { SearchCriteria } from '../../shared/components/search-bar/search-bar.component';
import { EventApplicationService } from '../../application/services/event-application.service';
import { ToastService } from '../../infrastructure/services/toast.service';
import { Event } from '../../core/models/event.model';

/**
 * Landing page: hero + categories + events grid.
 *
 * Loads events once on init via EventService and keeps local filter signals
 * for keyword/location/category. Derived `visibleEvents` is recomputed
 * automatically when any filter changes.
 */
@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, HeroComponent, HowItWorksComponent, CategoriesComponent, EventsGridComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-hero (search)="onSearch($event)" />
    <app-how-it-works />
    <app-categories (categorySelect)="onCategory($event)" />
    <app-events-grid [events]="visibleEvents()" [loading]="loading()" />
  `,
})
export class HomeComponent implements OnInit {
  private eventApp = inject(EventApplicationService);
  private toastService = inject(ToastService);

  /** All events fetched from the backend (unfiltered). */
  private readonly allEvents = signal<Event[]>([]);
  readonly loading = signal(false);

  // Active filters
  private readonly keyword = signal('');
  private readonly location = signal('');
  private readonly category = signal('');

  /** Derived list re-computed whenever events or any filter signal changes. */
  readonly visibleEvents = signal<Event[]>([]);

  ngOnInit(): void {
    this.loadEvents();
  }

  onSearch(criteria: SearchCriteria): void {
    this.keyword.set(criteria.keyword.toLowerCase());
    this.location.set(criteria.location.toLowerCase());
    this.recompute();
  }

  onCategory(category: string): void {
    this.category.set(category.toLowerCase());
    this.recompute();
  }

  private loadEvents(): void {
    this.loading.set(true);
    this.eventApp.getEvents().subscribe({
      next: (result) => {
        if (result.isSuccess && result.value) {
          this.allEvents.set(result.value);
        } else {
          this.toastService.showError(result.errors?.[0]?.message ?? 'Failed to load events.');
        }
        this.loading.set(false);
        this.recompute();
      },
      error: () => {
        this.toastService.showError('Could not reach the server. Please try again.');
        this.loading.set(false);
      },
    });
  }

  /** Recompute the filtered view from allEvents + current filter signals. */
  private recompute(): void {
    const kw = this.keyword();
    const loc = this.location();
    const cat = this.category();

    const filtered = this.allEvents().filter((e) => {
      const matchesKeyword = !kw || e.title.toLowerCase().includes(kw) || e.description.toLowerCase().includes(kw);
      const matchesLocation = !loc || e.location.toLowerCase().includes(loc);
      const matchesCategory = !cat || e.type.toLowerCase() === cat;
      return matchesKeyword && matchesLocation && matchesCategory;
    });

    this.visibleEvents.set(filtered);
  }
}
