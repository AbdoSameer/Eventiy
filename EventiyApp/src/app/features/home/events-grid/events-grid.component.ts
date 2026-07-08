import { ChangeDetectionStrategy, Component, Input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Event } from '../../../core/models/event.model';
import { EventCardComponent } from '../../../shared/components/event-card/event-card.component';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { RouterLink } from '@angular/router';

/**
 * Meetup-inspired events section with header + responsive grid.
 *
 * Shows a "Popular events" heading with a "See all" link.
 * Three states: loading (skeleton), empty, and data grid.
 */
@Component({
  selector: 'app-events-grid',
  standalone: true,
  imports: [CommonModule, EventCardComponent, SkeletonLoaderComponent, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section id="events" class="events-section">
      <div class="events-header">
        <h2 class="events-title">Popular events</h2>
        <a routerLink="/events" class="events-see-all">See all events →</a>
      </div>

      @if (loading) {
        <app-skeleton-loader type="card" />
      } @else if (events.length === 0) {
        <div class="events-empty">
          <div class="events-empty-icon">
          <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z" />
          </svg>
        </div>
          <h3 class="events-empty-title">No events found</h3>
          <p class="events-empty-subtitle">Try a different search or category.</p>
        </div>
      } @else {
        <div class="events-grid">
          @for (event of events; track event.id) {
            <app-event-card [event]="event" />
          }
        </div>
      }
    </section>
  `,
  styles: [`
    /* ── Events Section ─────────────────────────────── */
    .events-section {
      max-width: 1280px;
      margin: 0 auto;
      padding: 2rem 2rem 4rem;
    }

    .events-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 2rem;
    }

    .events-title {
      font-size: 1.5rem;
      font-weight: 700;
      color: #1F2937;
      margin: 0;
    }

    .events-see-all {
      font-size: 0.9375rem;
      font-weight: 600;
      color: #F6544C;
      text-decoration: none;
      transition: opacity 0.2s;
    }

    .events-see-all:hover {
      opacity: 0.75;
    }

    /* ── Events Grid ─────────────────────────────────── */
    .events-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
      gap: 1.5rem;
    }

    /* ── Empty State ─────────────────────────────────── */
    .events-empty {
      text-align: center;
      padding: 4rem 1rem;
    }

    .events-empty-icon {
      width: 3rem;
      height: 3rem;
      margin: 0 auto 1rem;
      color: #9CA3AF;
    }

    .events-empty-title {
      font-size: 1.25rem;
      font-weight: 600;
      color: #1F2937;
      margin: 0 0 0.5rem 0;
    }

    .events-empty-subtitle {
      font-size: 1rem;
      color: #6B7280;
      margin: 0;
    }

    /* ── Responsive ──────────────────────────────────── */
    @media (max-width: 640px) {
      .events-section {
        padding: 1.5rem 1rem 3rem;
      }

      .events-grid {
        grid-template-columns: 1fr;
      }
    }

    @media (min-width: 641px) and (max-width: 1024px) {
      .events-grid {
        grid-template-columns: repeat(2, 1fr);
      }
    }
  `],
})
export class EventsGridComponent {
  @Input() events: Event[] = [];
  @Input() loading = false;
}
