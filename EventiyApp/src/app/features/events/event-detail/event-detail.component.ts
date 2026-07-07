import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { EventApplicationService } from '../../../application/services/event-application.service';
import { BookingApplicationService } from '../../../application/services/booking-application.service';
import { AuthApplicationService } from '../../../application/services/auth-application.service';
import { ToastService } from '../../../infrastructure/services/toast.service';
import { Event as EventModel, EventPhotoResponse } from '../../../core/models/event.model';
import { DateFormatPipe } from '../../../shared/pipes/date-format.pipe';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { EventCardComponent } from '../../../shared/components/event-card/event-card.component';
import { PhotoUploaderComponent } from '../../../shared/components/photo-uploader/photo-uploader.component';
import { LightboxComponent } from '../../../shared/components/lightbox/lightbox.component';
import { ImgFallbackDirective } from '../../../infrastructure/directives/img-fallback.directive';

@Component({
  selector: 'app-event-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, DateFormatPipe, SkeletonLoaderComponent, EventCardComponent, PhotoUploaderComponent, LightboxComponent, ImgFallbackDirective],
  templateUrl: './event-detail.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EventDetailComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private eventApp = inject(EventApplicationService);
  private bookingApp = inject(BookingApplicationService);
  private auth = inject(AuthApplicationService);
  private toast = inject(ToastService);

  readonly event = signal<EventModel | null>(null);
  readonly relatedEvents = signal<EventModel[]>([]);
  readonly loading = signal(true);
  readonly booking = signal(false);

  readonly heroImageUrl = computed(() => {
    const evt = this.event();
    if (!evt) return undefined;
    return evt.coverPhotoUrl || evt.photos?.[0]?.publicUrl || undefined;
  });

  readonly selectedHeroPhoto = signal<EventPhotoResponse | null>(null);
  readonly activeHeroUrl = computed(() =>
    this.selectedHeroPhoto()?.publicUrl ?? this.heroImageUrl()
  );

  readonly lightboxOpen = signal(false);

  readonly quantity = signal(1);
  selectedTicketTypeId = '';

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.loading.set(false);
      return;
    }
    this.loadEvent(id);
  }

  private loadEvent(id: string): void {
    this.loading.set(true);
    this.eventApp.getEvent(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        if (result.isSuccess && result.value) {
          const evt = result.value;
          this.event.set(evt);
          this.selectedTicketTypeId = evt.ticketTypes?.[0]?.id ?? '';
          this.loadRelated(evt);
        } else {
          this.toast.showError(result.errors?.[0]?.message ?? 'Event not found.');
        }
        this.loading.set(false);
      },
      error: () => {
        this.toast.showError('Failed to load the event.');
        this.loading.set(false);
      },
    });
  }

  private loadRelated(evt: EventModel): void {
    this.eventApp.getEvents().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((result) => {
      if (result.isSuccess && result.value) {
        this.relatedEvents.set(result.value.filter((e) => e.id !== evt.id).slice(0, 3));
      }
    });
  }

  canBook(): boolean {
    const role = this.auth.userRole();
    return role === null || role === 'Attendee';
  }

  increaseQty(): void {
    this.quantity.update((q) => q + 1);
  }

  decreaseQty(): void {
    this.quantity.update((q) => Math.max(1, q - 1));
  }

  total(): number {
    const evt = this.event();
    if (!evt) return 0;
    const ticket = evt.ticketTypes?.find((t) => t.id === this.selectedTicketTypeId);
    const unitPrice = ticket ? ticket.price : evt.price;
    return (unitPrice ?? 0) * this.quantity();
  }

  book(): void {
    const evt = this.event();
    if (!evt) return;

    if (!this.auth.isAuthenticated()) {
      this.toast.showInfo('Please log in to complete your booking.');
      this.router.navigate(['/login'], { queryParams: { returnUrl: `/events/${evt.id}` } });
      return;
    }

    if (!this.selectedTicketTypeId) {
      this.toast.showError('Please select a ticket type.');
      return;
    }

    this.booking.set(true);
    this.bookingApp
      .createBooking({
        eventId: evt.id,
        ticketTypeId: this.selectedTicketTypeId,
        quantity: this.quantity(),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.booking.set(false);
          if (result.isSuccess && result.value) {
            this.toast.showSuccess('Booking confirmed! Find it in My Bookings.');
            this.router.navigateByUrl('/dashboard/attendee');
          } else if (result.isFailure) {
            this.toast.showError(result.errors?.[0]?.message ?? 'Booking failed.');
          }
        },
        error: () => this.booking.set(false),
      });
  }

  capacityPercent(): number {
    const evt = this.event();
    if (!evt || !evt.capacity) return 0;
    return Math.min(100, Math.round((evt.attendeeCount / evt.capacity) * 100));
  }

  // ===== Photo helpers =====

  selectHeroPhoto(photo: EventPhotoResponse): void {
    this.selectedHeroPhoto.set(photo);
  }

  openGalleryLightbox(index: number): void {
    const photos = this.event()?.photos;
    if (!photos || photos.length === 0) return;
    const rotated = [...photos.slice(index), ...photos.slice(0, index)];
    this.event.set({ ...this.event()!, photos: rotated });
    this.lightboxOpen.set(true);
  }

  closeLightbox(): void {
    this.lightboxOpen.set(false);
  }

  onPhotosChanged(photos: EventPhotoResponse[]): void {
    const evt = this.event();
    if (evt) {
      this.event.set({ ...evt, photos });
    }
  }
}
