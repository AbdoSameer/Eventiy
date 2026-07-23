import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, UntypedFormArray, UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthApplicationService } from '../../../application/services/auth-application.service';
import { EventApplicationService } from '../../../application/services/event-application.service';
import { ToastService } from '../../../infrastructure/services/toast.service';
import { AddTicketTypeRequest, EVENT_CATEGORIES } from '../../../core/models/event.model';
import type { EventStatus } from '../../../core/models/event.model';

@Component({
  selector: 'app-event-edit',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-background-alt min-h-screen py-10">
      <div class="max-w-3xl mx-auto px-4 sm:px-6 lg:px-8">
        <header class="mb-8">
          <h1 class="text-3xl font-bold text-text-primary">Edit Event</h1>
          <p class="text-text-secondary mt-1">Organizers can edit draft events; administrators can edit any event.</p>
        </header>

        <form [formGroup]="form" (ngSubmit)="updateEvent()" class="bg-white rounded-2xl shadow-md p-6 sm:p-8 mb-8">
          <h2 class="text-xl font-bold text-text-primary mb-4">Event details</h2>

          <div class="space-y-4">
            <div>
              <label for="name" class="block text-sm font-semibold text-text-primary mb-1.5">Event name *</label>
              <input
                id="name"
                type="text"
                formControlName="name"
                maxlength="100"
                class="w-full rounded-lg border border-gray-300 px-4 py-3 focus:border-primary focus:ring-primary"
                [class.border-red-500]="invalid('name')"
                [disabled]="!canEdit"
              />
              @if (invalid('name')) {
                <p class="mt-1 text-sm text-red-600">Name is required (max 100 chars).</p>
              }
            </div>

            <div>
              <label for="description" class="block text-sm font-semibold text-text-primary mb-1.5">Description</label>
              <textarea
                id="description"
                formControlName="description"
                rows="5"
                class="w-full rounded-lg border border-gray-300 px-4 py-3 focus:border-primary focus:ring-primary"
                [disabled]="!canEdit"
              ></textarea>
            </div>

            <div>
              <label for="street" class="block text-sm font-semibold text-text-primary mb-1.5">Street</label>
              <input id="street" type="text" formControlName="street" class="w-full rounded-lg border border-gray-300 px-4 py-3" [disabled]="!canEdit" />
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label for="city" class="block text-sm font-semibold text-text-primary mb-1.5">City</label>
                <input id="city" type="text" formControlName="city" class="w-full rounded-lg border border-gray-300 px-4 py-3" [disabled]="!canEdit" />
              </div>
              <div>
                <label for="country" class="block text-sm font-semibold text-text-primary mb-1.5">Country</label>
                <input id="country" type="text" formControlName="country" class="w-full rounded-lg border border-gray-300 px-4 py-3" [disabled]="!canEdit" />
              </div>
            </div>

            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label for="latitude" class="block text-sm font-semibold text-text-primary mb-1.5">Latitude</label>
                <input id="latitude" type="number" step="any" formControlName="latitude" class="w-full rounded-lg border border-gray-300 px-4 py-3" [disabled]="!canEdit" />
              </div>
              <div>
                <label for="longitude" class="block text-sm font-semibold text-text-primary mb-1.5">Longitude</label>
                <input id="longitude" type="number" step="any" formControlName="longitude" class="w-full rounded-lg border border-gray-300 px-4 py-3" [disabled]="!canEdit" />
              </div>
            </div>

            <div>
              <label for="category" class="block text-sm font-semibold text-text-primary mb-1.5">Category *</label>
              <select id="category" formControlName="category" class="w-full rounded-lg border border-gray-300 px-4 py-3 focus:border-primary focus:ring-primary" [disabled]="!canEdit">
                @for (cat of categories; track cat) {
                  <option [value]="cat">{{ cat }}</option>
                }
              </select>
            </div>

            <div>
              <label for="capacity" class="block text-sm font-semibold text-text-primary mb-1.5">Capacity</label>
              <input id="capacity" type="number" min="1" formControlName="capacity" class="w-full rounded-lg border border-gray-300 px-4 py-3" [disabled]="!canEdit" />
            </div>
          </div>

          @if (canEdit) {
            <div class="mt-8 pt-6 border-t border-border flex items-center justify-between">
              <button
                type="submit"
                [disabled]="saving()"
                class="rounded-full bg-primary text-white px-6 py-2.5 font-semibold hover:bg-primary-dark transition-colors disabled:opacity-60"
              >
                {{ saving() ? 'Saving…' : 'Save Changes' }}
              </button>
              @if (isAdmin && eventStatus === 'Draft') {
                <button
                  type="button"
                  (click)="publishEvent()"
                  [disabled]="publishing()"
                  class="rounded-full bg-green-600 text-white px-6 py-2.5 font-semibold hover:bg-green-700 transition-colors disabled:opacity-60"
                >
                  {{ publishing() ? 'Publishing…' : 'Publish Event' }}
                </button>
              }
            </div>
          }
        </form>

        @if (eventStatus === 'Published') {
          <div class="bg-white rounded-2xl shadow-md p-6 sm:p-8 mb-8">
            <h2 class="text-xl font-bold text-text-primary mb-4">Inventory mode</h2>
            <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
              <div class="flex-1">
                <p class="text-text-secondary mb-1">
                  Switch inventory management between standard SQL and high-performance Redis mode.
                </p>
                <p class="text-sm text-yellow-700 bg-yellow-50 border border-yellow-200 rounded-lg px-3 py-2">
                  ⚠ Enabling High-Demand mode switches seat/inventory reservations to Redis for faster throughput during peak periods.
                </p>
              </div>
              <button
                type="button"
                (click)="toggleHighDemand()"
                [disabled]="togglingHighDemand()"
                class="inline-flex items-center gap-2 rounded-full px-5 py-2.5 font-semibold border-2 transition-colors whitespace-nowrap disabled:opacity-60 self-start"
                [class]="highDemand()
                  ? 'bg-red-100 text-red-700 border-red-300 hover:bg-red-200'
                  : 'bg-gray-100 text-gray-600 border-gray-300 hover:bg-gray-200'"
              >
                @if (togglingHighDemand()) {
                  <span aria-hidden="true">…</span>
                  <span>Toggling…</span>
                } @else if (highDemand()) {
                  <span aria-hidden="true">⚠</span>
                  <span>High-Demand: ON</span>
                } @else {
                  <span>High-Demand: OFF</span>
                }
              </button>
            </div>
          </div>
        }

        @if (canEdit) {
          <div class="bg-white rounded-2xl shadow-md p-6 sm:p-8">
            <h2 class="text-xl font-bold text-text-primary mb-4">Ticket types</h2>
            <p class="text-text-secondary mb-4">Add new ticket tiers for this event.</p>

            <div [formGroup]="ticketTypesForm">
              <div formArrayName="tickets" class="space-y-4 mb-6">
              @for (ticket of ticketTypes.controls; track $index; let i = $index) {
                <div [formGroupName]="i" class="grid grid-cols-1 sm:grid-cols-[1fr_120px_100px_120px_auto] gap-3 items-end bg-background-alt rounded-xl p-4">
                  <div>
                    <label class="block text-xs font-semibold text-text-primary mb-1">Name</label>
                    <input type="text" formControlName="name" class="w-full rounded-lg border border-gray-300 px-3 py-2 focus:border-primary focus:ring-primary" placeholder="VIP" />
                  </div>
                  <div>
                    <label class="block text-xs font-semibold text-text-primary mb-1">Price</label>
                    <input type="number" min="0" step="0.01" formControlName="amount" class="w-full rounded-lg border border-gray-300 px-3 py-2 focus:border-primary focus:ring-primary" />
                  </div>
                  <div>
                    <label class="block text-xs font-semibold text-text-primary mb-1">Currency</label>
                    <input type="text" formControlName="currency" class="w-full rounded-lg border border-gray-300 px-3 py-2 focus:border-primary focus:ring-primary" />
                  </div>
                  <div>
                    <label class="block text-xs font-semibold text-text-primary mb-1">Capacity</label>
                    <input type="number" min="1" formControlName="capacity" class="w-full rounded-lg border border-gray-300 px-3 py-2 focus:border-primary focus:ring-primary" />
                  </div>
                  <button
                    type="button"
                    (click)="removeTicket(i)"
                    class="rounded-full px-3 py-2 text-red-600 hover:bg-red-50"
                    aria-label="Remove ticket type"
                  >
                    ✕
                  </button>
                </div>
              }
            </div>

            <button
              type="button"
              (click)="addTicket()"
              class="rounded-full border-2 border-primary text-primary px-5 py-2 font-semibold hover:bg-primary hover:text-white transition-colors"
            >
              + Add ticket type
            </button>

            @if (ticketTypes.length > 0) {
              <div class="flex items-center justify-between mt-8 pt-6 border-t border-border">
                <button
                  type="button"
                  (click)="addTickets()"
                  [disabled]="addingTickets()"
                  class="rounded-full bg-primary text-white px-6 py-2.5 font-semibold hover:bg-primary-dark transition-colors disabled:opacity-60"
                >
                  {{ addingTickets() ? 'Saving…' : 'Save Ticket Types' }}
                </button>
              </div>
            }
            </div>
          </div>
        }
      </div>
    </div>
  `,
})
export class EventEditComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private fb = inject(UntypedFormBuilder);
  private auth = inject(AuthApplicationService);
  private eventApp = inject(EventApplicationService);
  private toast = inject(ToastService);

  private eventId = '';
  protected readonly categories = EVENT_CATEGORIES;
  readonly saving = signal(false);
  readonly addingTickets = signal(false);
  readonly publishing = signal(false);
  readonly highDemand = signal(false);
  readonly togglingHighDemand = signal(false);

  protected eventStatus: EventStatus | null = null;

  get isAdmin(): boolean {
    return this.auth.userRole() === 'Admin';
  }

  get canEdit(): boolean {
    return this.isAdmin || (this.auth.userRole() === 'Organizer' && this.eventStatus === 'Draft');
  }

  private eventDate = '';
  private eventStreet = '';

  readonly form: UntypedFormGroup = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: [''],
    city: [''],
    country: [''],
    street: [''],
    latitude: [null],
    longitude: [null],
    category: ['Music', [Validators.required]],
    capacity: [0, [Validators.min(1)]],
  });

  ticketTypesForm: UntypedFormGroup = this.fb.group({
    tickets: this.fb.array([]),
  });

  get ticketTypes(): UntypedFormArray {
    return this.ticketTypesForm.get('tickets') as UntypedFormArray;
  }

  ngOnInit(): void {
    this.eventId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.eventId) {
      this.toast.showError('Event not found.');
      this.router.navigateByUrl('/events');
      return;
    }

    this.eventApp.getEventDetails(this.eventId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        if (result.isSuccess && result.value) {
          this.eventDate = result.value.date;
          this.eventStreet = result.value.location.street;
          this.eventStatus = result.value.status;
          this.highDemand.set(result.value.isHighDemand ?? false);
          this.form.patchValue({
            name: result.value.name,
            description: result.value.description,
            city: result.value.location.city,
            country: result.value.location.country,
            street: result.value.location.street,
            latitude: result.value.location.latitude ?? null,
            longitude: result.value.location.longitude ?? null,
            category: result.value.type || 'Music',
            capacity: result.value.capacity,
          });
        } else {
          this.toast.showError('Failed to load event details.');
        }
      },
      error: () => this.toast.showError('Could not reach the server.'),
    });
  }

  updateEvent(): void {
    if (this.form.invalid) return;
    this.saving.set(true);

    const { name, description, city, country, street, latitude, longitude, category, capacity } = this.form.value;
    const data = {
      name,
      capacity,
      date: this.eventDate,
      location: { country, city, street: street || this.eventStreet, latitude, longitude },
      description,
      type: category,
      latitude,
      longitude,
    };

    this.eventApp.updateEvent(this.eventId, data).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        this.saving.set(false);
        if (result.isSuccess) {
          this.toast.showSuccess('Event updated successfully.');
        } else {
          this.toast.showError(result.errors?.[0]?.message ?? 'Failed to update event.');
        }
      },
      error: () => {
        this.saving.set(false);
        this.toast.showError('Could not reach the server.');
      },
    });
  }

  publishEvent(): void {
    this.publishing.set(true);
    this.eventApp.publishEvent(this.eventId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        this.publishing.set(false);
        if (result.isSuccess) {
          this.eventStatus = 'Published';
          this.toast.showSuccess('Event published successfully.');
        } else {
          this.toast.showError(result.errors?.[0]?.message ?? 'Failed to publish event.');
        }
      },
      error: () => {
        this.publishing.set(false);
        this.toast.showError('Could not reach the server.');
      },
    });
  }

  toggleHighDemand(): void {
    this.togglingHighDemand.set(true);
    const enabled = !this.highDemand();
    this.eventApp.setHighDemand(this.eventId, enabled).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        this.togglingHighDemand.set(false);
        if (result.isSuccess && result.value) {
          this.highDemand.set(result.value.isHighDemand);
          this.toast.showSuccess(
            result.value.isHighDemand
              ? 'High-demand mode enabled. Inventory now runs on Redis.'
              : 'High-demand mode disabled. Inventory reverted to SQL.',
          );
        } else {
          this.toast.showError(result.errors?.[0]?.message ?? 'Could not toggle high-demand mode.');
        }
      },
      error: () => {
        this.togglingHighDemand.set(false);
        this.toast.showError('Could not reach the server.');
      },
    });
  }

  addTicket(): void {
    this.ticketTypes.push(
      this.fb.group({
        name: ['', [Validators.required]],
        amount: [10, [Validators.required, Validators.min(1)]],
        currency: ['USD', [Validators.required]],
        capacity: [1, [Validators.required, Validators.min(1)]],
      }),
    );
  }

  removeTicket(index: number): void {
    this.ticketTypes.removeAt(index);
  }

  addTickets(): void {
    const tickets = this.ticketTypes.value as AddTicketTypeRequest[];
    if (tickets.length === 0) return;

    this.addingTickets.set(true);
    let completed = 0;
    let hasError = false;

    const addFn = (t: AddTicketTypeRequest) => this.eventApp.addTicketType(this.eventId, t);

    for (const ticket of tickets) {
      addFn(ticket).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: (result) => {
          completed++;
          if (result.isFailure) {
            hasError = true;
            this.toast.showError(result.errors?.[0]?.message ?? 'Failed to add a ticket type.');
          }
          if (completed === tickets.length && !hasError) {
            this.toast.showSuccess('Ticket types added successfully.');
            this.ticketTypesForm.reset();
            while (this.ticketTypes.length > 0) {
              this.ticketTypes.removeAt(0);
            }
          }
        },
        error: () => {
          completed++;
        },
      });
    }
  }

  invalid(field: string): boolean {
    const c = this.form.get(field);
    return !!c && c.invalid && (c.touched || c.dirty);
  }
}
