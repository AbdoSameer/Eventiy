import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Event } from '../../../core/models/event.model';
import { EventCardComponent } from '../../../shared/components/event-card/event-card.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';

/**
 * Responsive grid of event cards with loading + empty states.
 * Pure presentational — data and loading flag come from the parent.
 */
@Component({
  selector: 'app-events-grid',
  standalone: true,
  imports: [CommonModule, EventCardComponent, SkeletonLoaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div id="events" class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-16 scroll-mt-20">
      @if (loading) {
        <app-skeleton-loader type="card" />
      } @else if (events.length === 0) {
        <div class="text-center py-20">
          <p class="text-6xl mb-4" aria-hidden="true">🔍</p>
          <h3 class="text-xl font-semibold text-text-primary mb-2">No events found</h3>
          <p class="text-text-secondary">Try a different search or category.</p>
        </div>
      } @else {
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          @for (event of events; track event.id) {
            <app-event-card [event]="event" />
          }
        </div>
      }
    </div>
  `,
})
export class EventsGridComponent {
  @Input() events: Event[] = [];
  @Input() loading = false;
}
