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
