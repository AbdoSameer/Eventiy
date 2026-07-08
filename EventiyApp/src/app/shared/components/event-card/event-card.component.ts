import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Event as EventModel } from '../../../core/models/event.model';
import { DateFormatPipe } from '../../pipes/date-format.pipe';
import { ImgFallbackDirective } from '../../../infrastructure/directives/img-fallback.directive';

@Component({
  selector: 'app-event-card',
  standalone: true,
  imports: [CommonModule, DateFormatPipe, ImgFallbackDirective],
  templateUrl: './event-card.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    /* ── Event Card (Horizontal Layout) ─────────────── */
    .event-card {
      display: flex;
      background: #ffffff;
      border-radius: 1rem;
      border: 1px solid #E5E7EB;
      overflow: hidden;
      cursor: pointer;
      transition: transform 0.2s ease, box-shadow 0.2s ease;
    }

    .event-card:hover {
      transform: translateY(-3px);
      box-shadow: 0 12px 30px rgba(0, 0, 0, 0.1);
    }

    /* ── Image ────────────────────────────────────────── */
    .event-card-image {
      flex-shrink: 0;
      width: 200px;
      overflow: hidden;
    }

    .event-card-img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      transition: transform 0.3s ease;
    }

    .event-card:hover .event-card-img {
      transform: scale(1.05);
    }

    .event-card-img-placeholder {
      width: 100%;
      height: 100%;
      min-height: 200px;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, #f97316, #F6544C);
    }

    .event-card-img-placeholder svg {
      width: 2.5rem;
      height: 2.5rem;
      color: rgba(255, 255, 255, 0.7);
    }

    /* ── Body ────────────────────────────────────────── */
    .event-card-body {
      flex: 1;
      padding: 1.25rem;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      min-width: 0;
    }

    .event-card-title {
      font-size: 1.0625rem;
      font-weight: 700;
      color: #1F2937;
      margin: 0;
      line-height: 1.35;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }

    /* ── Meta (Date & Location) ──────────────────────── */
    .event-card-meta {
      display: flex;
      flex-direction: column;
      gap: 0.375rem;
    }

    .event-card-meta-item {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.875rem;
      color: #6B7280;
    }

    .event-card-meta-icon {
      width: 1rem;
      height: 1rem;
      flex-shrink: 0;
      color: #9CA3AF;
    }

    /* ── Footer (Avatars + Actions) ──────────────────── */
    .event-card-footer {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-top: auto;
      padding-top: 0.75rem;
    }

    .event-card-attendees {
      display: flex;
      align-items: center;
      gap: 0.625rem;
    }

    .avatar-stack {
      display: flex;
    }

    .avatar {
      width: 1.75rem;
      height: 1.75rem;
      border-radius: 9999px;
      background: #D1D5DB;
      border: 2px solid #ffffff;
      margin-right: -0.5rem;
    }

    .avatar-2 {
      background: #9CA3AF;
      z-index: 1;
    }

    .avatar-3 {
      background: #6B7280;
      z-index: 2;
      margin-right: 0;
    }

    .attendee-count {
      font-size: 0.8125rem;
      font-weight: 500;
      color: #6B7280;
    }

    .event-card-actions {
      display: flex;
      align-items: center;
      gap: 0.625rem;
    }

    .price-badge {
      display: inline-flex;
      align-items: center;
      padding: 0.375rem 0.875rem;
      border-radius: 9999px;
      font-size: 0.8125rem;
      font-weight: 700;
      background: #FEF2F2;
      color: #E53E3E;
    }

    .share-btn {
      width: 2rem;
      height: 2rem;
      border-radius: 9999px;
      border: 1px solid #E5E7EB;
      background: #ffffff;
      display: flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      transition: all 0.2s;
      color: #6B7280;
    }

    .share-btn:hover {
      border-color: #F6544C;
      color: #F6544C;
      background: #FEF2F2;
    }

    .share-btn svg {
      width: 1rem;
      height: 1rem;
    }

    /* ── Responsive: Stack vertically on mobile ──────── */
    @media (max-width: 640px) {
      .event-card {
        flex-direction: column;
      }

      .event-card-image {
        width: 100%;
        height: 180px;
      }

      .event-card-img-placeholder {
        min-height: 180px;
      }
    }
  `],
})
export class EventCardComponent {
  readonly event = input.required<EventModel>();

  private router = inject(Router);

  get priceLabel(): string {
    const p = this.event().price;
    if (!p || p === 0) return 'Free';
    return `${p.toLocaleString('en-US', { style: 'currency', currency: this.event().currency ?? 'USD', minimumFractionDigits: 0 })}`;
  }

  get attendeeLabel(): string {
    const count = this.event().attendeeCount ?? 0;
    return `${count} ${count === 1 ? 'attendee' : 'attendees'}`;
  }

  open(): void {
    this.router.navigateByUrl(`/events/${this.event().id}`);
  }

  share(event: MouseEvent): void {
    event.stopPropagation();
  }
}
