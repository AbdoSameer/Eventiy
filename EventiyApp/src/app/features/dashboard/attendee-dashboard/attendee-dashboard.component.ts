import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { BookingApplicationService } from '../../../application/services/booking-application.service';
import { ToastService } from '../../../infrastructure/services/toast.service';
import { Booking, BookingStatus } from '../../../core/models/booking.model';
import { DateFormatPipe } from '../../../shared/pipes/date-format.pipe';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';
import { CATEGORY_META } from '../../../core/models/event.model';

@Component({
  selector: 'app-attendee-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, DateFormatPipe, SkeletonLoaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-background-alt min-h-screen">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
        <header class="mb-8">
          <h1 class="text-3xl font-bold text-text-primary">My Bookings</h1>
          <p class="text-text-secondary mt-1">All the events you've booked — past and upcoming.</p>
        </header>

        @if (loading()) {
          <app-skeleton-loader type="list" />
        } @else if (bookings().length === 0) {
          <div class="bg-white rounded-2xl shadow-md p-12 text-center">
            <p class="text-6xl mb-4" aria-hidden="true">🎟️</p>
            <h3 class="text-xl font-semibold text-text-primary mb-1">No bookings yet</h3>
            <p class="text-text-secondary mb-6">Find an event you love and book your spot.</p>
            <a routerLink="/events" class="inline-block rounded-full bg-primary text-white px-6 py-3 font-semibold hover:bg-primary-dark transition-colors">
              Browse events
            </a>
          </div>
        } @else {
          <div class="space-y-4">
            @for (booking of bookings(); track booking.id) {
              <article class="bg-white rounded-2xl shadow-md hover:shadow-lg transition-shadow p-4 sm:p-6 flex flex-col sm:flex-row gap-5">
                <!-- Event image placeholder -->
                <div class="w-full sm:w-40 h-32 sm:h-32 rounded-xl bg-gradient-to-br from-primary-light to-primary flex items-center justify-center text-4xl text-white shrink-0 overflow-hidden">
                  <span aria-hidden="true">{{ emoji(booking) }}</span>
                </div>

                <!-- Body -->
                <div class="flex-1 flex flex-col sm:flex-row sm:items-center gap-4">
                  <div class="flex-1">
                    <div class="flex items-center gap-3 flex-wrap">
                      <h3 class="text-lg font-semibold text-text-primary">{{ booking.eventTitle }}</h3>
                      <span class="rounded-full px-3 py-1 text-xs font-semibold" [class]="statusClass(booking.status)">{{ booking.status }}</span>
                    </div>
                    <dl class="mt-2 grid grid-cols-2 gap-x-6 gap-y-1 text-sm text-text-secondary">
                      <div>
                        <dt class="inline">Ticket:</dt>
                        <dd class="inline ml-1 font-medium text-text-primary">{{ booking.ticketTypeName }}</dd>
                      </div>
                      <div>
                        <dt class="inline">Quantity:</dt>
                        <dd class="inline ml-1 font-medium text-text-primary">{{ booking.quantity }}</dd>
                      </div>
                      <div>
                        <dt class="inline">Total:</dt>
                        <dd class="inline ml-1 font-bold text-primary">{{ booking.totalAmount | currency:'USD' }}</dd>
                      </div>
                      @if (booking.eventDate) {
                        <div>
                          <dt class="inline">When:</dt>
                          <dd class="inline ml-1 font-medium text-text-primary">{{ booking.eventDate | dateFormat }}</dd>
                        </div>
                      }
                    </dl>
                  </div>

                  <a
                    [routerLink]="['/events', booking.eventId]"
                    class="self-start sm:self-center rounded-full border-2 border-primary text-primary px-5 py-2 font-semibold hover:bg-primary hover:text-white transition-colors"
                  >
                    View Event
                  </a>
                </div>
              </article>
            }
          </div>
        }
      </div>
    </div>
  `,
})
export class AttendeeDashboardComponent implements OnInit {
  private bookingApp = inject(BookingApplicationService);
  private toast = inject(ToastService);

  readonly bookings = signal<Booking[]>([]);
  readonly loading = signal(false);

  ngOnInit(): void {
    this.loadBookings();
  }

  private loadBookings(): void {
    this.loading.set(true);
    this.bookingApp.getMyBookings().subscribe({
      next: (result) => {
        if (result.isSuccess && result.value) {
          this.bookings.set(result.value);
        } else {
          this.toast.showError(result.errors?.[0]?.message ?? 'Failed to load your bookings.');
        }
        this.loading.set(false);
      },
      error: () => {
        this.toast.showError('Could not reach the server.');
        this.loading.set(false);
      },
    });
  }

  statusClass(status: BookingStatus): string {
    switch (status) {
      case 'Confirmed':
        return 'bg-green-100 text-green-700';
      case 'Cancelled':
        return 'bg-gray-200 text-gray-700';
      case 'Refunded':
        return 'bg-purple-100 text-purple-700';
      case 'Expired':
        return 'bg-orange-100 text-orange-700';
      default:
        return 'bg-yellow-100 text-yellow-700';
    }
  }

  emoji(booking: Booking): string {
    const title = booking.eventTitle.toLowerCase();
    for (const key of Object.keys(CATEGORY_META)) {
      if (title.includes(key.toLowerCase())) {
        return CATEGORY_META[key].emoji;
      }
    }
    return '🎟️';
  }
}