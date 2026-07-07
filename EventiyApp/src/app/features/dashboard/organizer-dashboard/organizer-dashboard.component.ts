import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { EventApplicationService } from '../../../application/services/event-application.service';
import { BookingApplicationService } from '../../../application/services/booking-application.service';
import { ToastService } from '../../../infrastructure/services/toast.service';
import { Event } from '../../../core/models/event.model';
import { Booking, BookingStatus, BookingByEventResponse } from '../../../core/models/booking.model';
import { DateFormatPipe } from '../../../shared/pipes/date-format.pipe';
import { SkeletonLoaderComponent } from '../../../shared/components/skeleton-loader/skeleton-loader.component';

type Tab = 'events' | 'bookings' | 'analytics';

@Component({
  selector: 'app-organizer-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, DateFormatPipe, SkeletonLoaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-background-alt min-h-screen">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
        <header class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 mb-8">
          <div>
            <h1 class="text-3xl font-bold text-text-primary">Organizer dashboard</h1>
            <p class="text-text-secondary mt-1">Manage your events and bookings.</p>
          </div>
          <a
            routerLink="/events/create"
            class="rounded-full bg-primary text-white px-6 py-3 font-semibold hover:bg-primary-dark transition-colors self-start"
          >
            + Create New Event
          </a>
        </header>

        <!-- Tabs -->
        <div class="flex gap-1 border-b border-border mb-8 overflow-x-auto">
          @for (tab of tabs; track tab.id) {
            <button
              (click)="setTab(tab.id)"
              class="px-5 py-3 font-semibold whitespace-nowrap transition-colors border-b-2 -mb-px"
              [class]="activeTab() === tab.id ? 'border-primary text-primary' : 'border-transparent text-text-secondary hover:text-text-primary'"
            >
              {{ tab.label }}
            </button>
          }
        </div>

        <!-- My Events tab -->
        @if (activeTab() === 'events') {
          @if (loading()) {
            <app-skeleton-loader type="list" />
          } @else if (events().length === 0) {
            <div class="bg-white rounded-2xl shadow-md p-12 text-center">
              <p class="text-5xl mb-3" aria-hidden="true">📅</p>
              <h3 class="text-xl font-semibold text-text-primary mb-1">No events yet</h3>
              <p class="text-text-secondary mb-6">Create your first event to get started.</p>
              <a routerLink="/events/create" class="inline-block rounded-full bg-primary text-white px-6 py-3 font-semibold hover:bg-primary-dark">Create event</a>
            </div>
          } @else {
            <div class="bg-white rounded-2xl shadow-md overflow-hidden overflow-x-auto">
              <table class="w-full text-left">
                <thead class="bg-background-alt text-text-secondary text-sm">
                  <tr>
                    <th class="px-5 py-3 font-semibold">Event</th>
                    <th class="px-5 py-3 font-semibold">Date</th>
                    <th class="px-5 py-3 font-semibold">Capacity</th>
                    <th class="px-5 py-3 font-semibold">Status</th>
                    <th class="px-5 py-3 font-semibold text-right">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  @for (event of events(); track event.id) {
                    <tr class="border-t border-border">
                      <td class="px-5 py-4">
                        <a [routerLink]="['/events', event.id]" class="font-semibold text-text-primary hover:text-primary">{{ event.title }}</a>
                        <p class="text-xs text-text-secondary">{{ safeCategory(event) }}{{ event.location ? ' • ' + event.location : '' }}</p>
                      </td>
                      <td class="px-5 py-4 text-text-secondary">{{ event.date | dateFormat }}</td>
                      <td class="px-5 py-4 text-text-secondary">{{ event.attendeeCount }} / {{ event.capacity }}</td>
                      <td class="px-5 py-4">
                        <span class="rounded-full px-3 py-1 text-xs font-semibold" [class]="eventStatusClass(event.status)">{{ event.status || 'Published' }}</span>
                      </td>
                      <td class="px-5 py-4 text-right space-x-2 whitespace-nowrap">
                        <a [routerLink]="['/events', event.id]" class="text-primary hover:underline text-sm font-semibold">Edit</a>
                        <button (click)="cancelEvent(event)" class="text-red-600 hover:underline text-sm font-semibold">Cancel</button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        }

        <!-- Bookings tab -->
        @if (activeTab() === 'bookings') {
          <div class="mb-4 flex items-center gap-3">
            <label for="statusFilter" class="text-sm text-text-secondary">Filter by status:</label>
            <select
              id="statusFilter"
              [(ngModel)]="statusFilter"
              class="rounded-lg border border-gray-300 px-3 py-2 text-text-primary focus:border-primary focus:ring-primary"
            >
              <option value="">All</option>
              <option value="Pending">Pending</option>
              <option value="Confirmed">Confirmed</option>
              <option value="Cancelled">Cancelled</option>
              <option value="Expired">Expired</option>
              <option value="Refunded">Refunded</option>
            </select>
          </div>

          @if (filteredBookings().length === 0) {
            <div class="bg-white rounded-2xl shadow-md p-12 text-center">
              <p class="text-5xl mb-3" aria-hidden="true">🎟️</p>
              <h3 class="text-xl font-semibold text-text-primary mb-1">No bookings here</h3>
              <p class="text-text-secondary">Bookings for your events will appear in this list.</p>
            </div>
          } @else {
            <div class="bg-white rounded-2xl shadow-md overflow-hidden overflow-x-auto">
              <table class="w-full text-left">
                <thead class="bg-background-alt text-text-secondary text-sm">
                  <tr>
                    <th class="px-5 py-3 font-semibold">Event</th>
                    <th class="px-5 py-3 font-semibold">Attendee</th>
                    <th class="px-5 py-3 font-semibold">Ticket</th>
                    <th class="px-5 py-3 font-semibold">Qty</th>
                    <th class="px-5 py-3 font-semibold">Amount</th>
                    <th class="px-5 py-3 font-semibold">Status</th>
                    <th class="px-5 py-3 font-semibold text-right">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  @for (booking of filteredBookings(); track booking.id) {
                    <tr class="border-t border-border">
                      <td class="px-5 py-4 font-medium text-text-primary">{{ booking.eventTitle }}</td>
                      <td class="px-5 py-4 text-text-secondary">{{ booking.attendeeName || '—' }}</td>
                      <td class="px-5 py-4 text-text-secondary">{{ booking.ticketTypeName }}</td>
                      <td class="px-5 py-4 text-text-secondary">{{ booking.quantity }}</td>
                      <td class="px-5 py-4 text-text-secondary">{{ booking.totalAmount | currency:'USD' }}</td>
                      <td class="px-5 py-4">
                        <span class="rounded-full px-3 py-1 text-xs font-semibold" [class]="statusClass(booking.status)">{{ booking.status }}</span>
                      </td>
                      <td class="px-5 py-4 text-right whitespace-nowrap">
                        @if (booking.status === 'Pending') {
                          <button (click)="confirmBooking(booking)" class="text-green-600 hover:underline text-sm font-semibold mr-3">Confirm</button>
                          <button (click)="cancelBooking(booking)" class="text-red-600 hover:underline text-sm font-semibold">Cancel</button>
                        } @else {
                          <span class="text-text-secondary text-sm">—</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        }

        <!-- Analytics tab -->
        @if (activeTab() === 'analytics') {
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
            <div class="bg-white rounded-2xl shadow-md p-6">
              <p class="text-sm text-text-secondary">Total events</p>
              <p class="text-3xl font-extrabold text-text-primary mt-1">{{ events().length }}</p>
            </div>
            <div class="bg-white rounded-2xl shadow-md p-6">
              <p class="text-sm text-text-secondary">Total attendees</p>
              <p class="text-3xl font-extrabold text-text-primary mt-1">{{ totalAttendees() }}</p>
            </div>
            <div class="bg-white rounded-2xl shadow-md p-6">
              <p class="text-sm text-text-secondary">Total capacity</p>
              <p class="text-3xl font-extrabold text-text-primary mt-1">{{ totalCapacity() }}</p>
            </div>
            <div class="bg-white rounded-2xl shadow-md p-6">
              <p class="text-sm text-text-secondary">Avg. fill rate</p>
              <p class="text-3xl font-extrabold text-primary mt-1">{{ fillRate() }}%</p>
            </div>
          </div>
        }
      </div>
    </div>
  `,
})
export class OrganizerDashboardComponent implements OnInit {
  private eventApp = inject(EventApplicationService);
  private bookingApp = inject(BookingApplicationService);
  private toast = inject(ToastService);

  protected readonly tabs: { id: Tab; label: string }[] = [
    { id: 'events', label: 'My Events' },
    { id: 'bookings', label: 'Bookings' },
    { id: 'analytics', label: 'Analytics' },
  ];

  readonly activeTab = signal<Tab>('events');
  readonly events = signal<Event[]>([]);
  readonly bookings = signal<Booking[]>([]);
  readonly loading = signal(false);
  statusFilter = '';

  readonly totalAttendees = computed(() => this.events().reduce((sum, e) => sum + (e.attendeeCount ?? 0), 0));
  readonly totalCapacity = computed(() => this.events().reduce((sum, e) => sum + (e.capacity ?? 0), 0));
  readonly fillRate = computed(() => {
    const cap = this.totalCapacity();
    if (!cap) {
      return 0;
    }
    return Math.round((this.totalAttendees() / cap) * 100);
  });

  readonly filteredBookings = computed(() => {
    const filter = this.statusFilter;
    if (!filter) {
      return this.bookings();
    }
    return this.bookings().filter((b) => b.status === filter);
  });

  ngOnInit(): void {
    this.loadEvents();
  }

  setTab(tab: Tab): void {
    this.activeTab.set(tab);
  }

  private loadEvents(): void {
    this.loading.set(true);
    this.eventApp.getEvents().subscribe({
      next: (result) => {
        if (result.isSuccess && result.value) {
          this.events.set(result.value);
          this.loadBookingsForEvents(result.value);
        } else {
          this.toast.showError(result.errors?.[0]?.message ?? 'Failed to load events.');
        }
        this.loading.set(false);
      },
      error: () => {
        this.toast.showError('Could not reach the server.');
        this.loading.set(false);
      },
    });
  }

  private loadBookingsForEvents(events: Event[]): void {
    if (events.length === 0) {
      return;
    }
    const eventMap = new Map(events.map((e) => [e.id, e]));
    const bookingRequests = events.slice(0, 5).map((event) =>
      this.bookingApp.getBookingsByEvent(event.id).pipe(
        map((result) => (result.isSuccess && result.value ? result.value : [])),
        catchError(() => of([])),
      ),
    );

    forkJoin(bookingRequests).subscribe((results) => {
      const allBookings: Booking[] = results.flat().map((b: BookingByEventResponse) => {
        const evt = eventMap.get(b.eventId);
        return {
          id: b.id,
          eventId: b.eventId,
          eventTitle: evt?.title ?? 'Unknown Event',
          ticketTypeName: '',
          quantity: b.quantity,
          totalAmount: b.totalAmount,
          status: 'Pending' as BookingStatus,
          createdAt: b.bookingDate,
        };
      });
      this.bookings.set(allBookings);
    });
  }

  cancelEvent(event: Event): void {
    if (!confirm(`Cancel "${event.title}"? This will remove it from the listings.`)) {
      return;
    }
    this.eventApp.deleteEvent(event.id).subscribe((result) => {
      if (result.isSuccess) {
        this.toast.showSuccess('Event cancelled.');
        this.events.update((list) => list.filter((e) => e.id !== event.id));
      } else {
        this.toast.showError(result.errors?.[0]?.message ?? 'Could not cancel the event.');
      }
    });
  }

  confirmBooking(booking: Booking): void {
    this.bookingApp.confirmBooking(booking.id).subscribe((result) => {
      if (result.isSuccess) {
        this.toast.showSuccess('Booking confirmed.');
        this.updateBookingStatus(booking.id, 'Confirmed');
      } else {
        this.toast.showError(result.errors?.[0]?.message ?? 'Could not confirm the booking.');
      }
    });
  }

  cancelBooking(booking: Booking): void {
    this.bookingApp.cancelBooking(booking.id).subscribe((result) => {
      if (result.isSuccess) {
        this.toast.showSuccess('Booking cancelled.');
        this.updateBookingStatus(booking.id, 'Cancelled');
      } else {
        this.toast.showError(result.errors?.[0]?.message ?? 'Could not cancel the booking.');
      }
    });
  }

  private updateBookingStatus(id: string, status: BookingStatus): void {
    this.bookings.update((list) => list.map((b) => (b.id === id ? { ...b, status } : b)));
  }

  statusClass(status: BookingStatus): string {
    switch (status) {
      case 'Confirmed':
        return 'bg-green-100 text-green-700';
      case 'Cancelled':
        return 'bg-red-100 text-red-700';
      case 'Refunded':
        return 'bg-purple-100 text-purple-700';
      case 'Expired':
        return 'bg-orange-100 text-orange-700';
      default:
        return 'bg-yellow-100 text-yellow-700';
    }
  }

  eventStatusClass(status?: string): string {
    switch (status) {
      case 'Published':
        return 'bg-green-100 text-green-700';
      case 'Draft':
        return 'bg-gray-200 text-gray-700';
      case 'Cancelled':
        return 'bg-red-100 text-red-700';
      case 'Completed':
        return 'bg-blue-100 text-blue-700';
      default:
        return 'bg-green-100 text-green-700';
    }
  }

  safeCategory(event: Event): string {
    return event.category ? event.category + ' • ' : '';
  }
}