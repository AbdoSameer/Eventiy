import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { BookingApplicationService } from '../../../application/services/booking-application.service';
import { BackendBookingDetails } from '../../../core/models/booking.model';
import { ToastService } from '../../../infrastructure/services/toast.service';
import { DateFormatPipe } from '../../../shared/pipes/date-format.pipe';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-booking-detail',
  standalone: true,
  imports: [RouterLink, CurrencyPipe, DatePipe, DateFormatPipe],
  template: `
    <div class="max-w-3xl mx-auto px-4 py-8">
      <button (click)="goBack()" class="text-primary hover:underline mb-4 block">&larr; Back</button>

      @if (loading()) {
        <p class="text-text-secondary">Loading booking details...</p>
      } @else if (error()) {
        <div class="bg-red-50 border border-red-200 rounded-xl p-6 text-center">
          <p class="text-red-700 font-semibold">{{ error() }}</p>
          <button (click)="goBack()" class="mt-3 text-primary hover:underline">Go back</button>
        </div>
      } @else if (b) {
        <article class="bg-white rounded-2xl shadow-md p-6 sm:p-8">
          <div class="flex items-center justify-between flex-wrap gap-3 mb-6">
            <h1 class="text-2xl font-bold text-text-primary">Booking Details</h1>
            <span class="rounded-full px-4 py-1.5 text-sm font-semibold" [class]="statusClass(b.status)">{{ b.status }}</span>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-x-8 gap-y-4 text-sm">
            <div><span class="text-text-secondary">Event:</span> <span class="font-semibold text-text-primary">{{ b.eventTitle }}</span></div>
            <div><span class="text-text-secondary">Booking ID:</span> <span class="font-mono text-xs text-text-primary">{{ b.id }}</span></div>
            <div><span class="text-text-secondary">Quantity:</span> <span class="font-semibold text-text-primary">{{ b.quantity }}</span></div>
            <div><span class="text-text-secondary">Total:</span> <span class="font-bold text-primary">{{ b.totalAmount | currency: b.currency }}</span></div>
            <div><span class="text-text-secondary">Payment:</span>
              <span class="rounded-full px-2.5 py-0.5 text-xs font-semibold"
                [class.bg-blue-100]="b.paymentMethod === 'Deferred'"
                [class.text-blue-700]="b.paymentMethod === 'Deferred'"
                [class.bg-green-100]="b.paymentMethod === 'Instant'"
                [class.text-green-700]="b.paymentMethod === 'Instant'"
              >
                {{ b.paymentMethod === 'Deferred' ? 'Pay Later' : 'Instant' }}
              </span>
            </div>
            @if (b.paymentMethod === 'Deferred' && b.referenceCode) {
              <div><span class="text-text-secondary">Ref. Code:</span> <span class="font-mono font-bold text-yellow-700">{{ b.referenceCode }}</span></div>
            }
            @if (b.paymentMethod === 'Deferred' && b.holdExpiresAt) {
              <div><span class="text-text-secondary">Hold Expires:</span> <span class="font-semibold text-text-primary">{{ b.holdExpiresAt | dateFormat }}</span></div>
            }
            <div><span class="text-text-secondary">Booked at:</span> <span class="font-semibold text-text-primary">{{ b.bookingDate | date:'medium' }}</span></div>
          </div>

          <div class="mt-8 flex flex-wrap gap-3">
            @if (b.status === 'Pending') {
              <button (click)="cancelBooking()" [disabled]="cancelling()"
                class="rounded-full border-2 border-red-400 text-red-500 px-6 py-2 font-semibold hover:bg-red-500 hover:text-white transition-colors disabled:opacity-60">
                {{ cancelling() ? 'Cancelling...' : 'Cancel Booking' }}
              </button>
            }
            <a [routerLink]="['/events', b.eventId]"
              class="rounded-full border-2 border-primary text-primary px-6 py-2 font-semibold hover:bg-primary hover:text-white transition-colors">
              View Event
            </a>
          </div>
        </article>
      }
    </div>
  `,
})
export class BookingDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly bookingApp = inject(BookingApplicationService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly booking = signal<BackendBookingDetails | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly cancelling = signal(false);

  get b(): BackendBookingDetails | null { return this.booking(); }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('No booking ID provided.');
      this.loading.set(false);
      return;
    }

    this.bookingApp.getBooking(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        if (result.isSuccess && result.value) {
          this.booking.set(result.value);
        } else {
          this.error.set(result.errors?.[0]?.message ?? 'Booking not found.');
        }
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load booking details.');
        this.loading.set(false);
      },
    });
  }

  cancelBooking(): void {
    const b = this.booking();
    if (!b) return;
    this.cancelling.set(true);
    this.bookingApp.cancelBooking(b.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        this.cancelling.set(false);
        if (result.isSuccess) {
          this.toast.showSuccess('Booking cancelled.');
          this.booking.update((x) => x ? { ...x, status: 'Cancelled' } : null);
        } else {
          this.toast.showError(result.errors?.[0]?.message ?? 'Cancellation failed.');
        }
      },
      error: () => {
        this.cancelling.set(false);
        this.toast.showError('Cancellation failed.');
      },
    });
  }

  goBack(): void {
    this.router.navigateByUrl('/dashboard/attendee');
  }

  statusClass(status: string): string {
    switch (status) {
      case 'Pending': return 'bg-yellow-100 text-yellow-700';
      case 'Confirmed': return 'bg-green-100 text-green-700';
      case 'Cancelled': return 'bg-red-100 text-red-700';
      default: return 'bg-gray-100 text-gray-600';
    }
  }
}
