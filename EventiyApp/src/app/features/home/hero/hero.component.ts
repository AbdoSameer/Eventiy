import { ChangeDetectionStrategy, Component, EventEmitter, Output } from '@angular/core';
import { SearchBarComponent, SearchCriteria } from '../../../shared/components/search-bar/search-bar.component';

/**
 * Landing hero. Emits `search` from the embedded SearchBar so the parent
 * HomeComponent can filter the events list (no direct service calls here).
 */
@Component({
  selector: 'app-hero',
  standalone: true,
  imports: [SearchBarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="relative overflow-hidden bg-gradient-to-b from-primary/5 via-background-alt to-white">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-20 lg:py-28 text-center">
        <h1 class="text-5xl md:text-6xl font-extrabold text-text-primary tracking-tight">
          Find your next event
        </h1>
        <p class="mt-5 text-lg md:text-xl text-text-secondary max-w-2xl mx-auto">
          Discover events that match your passions — music, tech, sports and more.
        </p>

        <div class="mt-10 max-w-3xl mx-auto">
          <app-search-bar (search)="onSearch($event)" />
        </div>

        <a
          href="#events"
          class="mt-8 inline-block rounded-full bg-primary text-white px-8 py-3 font-semibold hover:bg-primary-dark transition-colors"
        >
          Explore events
        </a>
      </div>
    </section>
  `,
})
export class HeroComponent {
  @Output() search = new EventEmitter<SearchCriteria>();

  onSearch(criteria: SearchCriteria): void {
    this.search.emit(criteria);
  }
}
