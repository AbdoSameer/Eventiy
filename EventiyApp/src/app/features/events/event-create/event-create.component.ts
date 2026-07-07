import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, UntypedFormArray, UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { EventApplicationService } from '../../../application/services/event-application.service';
import { ToastService } from '../../../infrastructure/services/toast.service';
import { PhotoUploaderComponent } from '../../../shared/components/photo-uploader/photo-uploader.component';
import { AddTicketTypeRequest, EventPhotoResponse, EVENT_CATEGORIES } from '../../../core/models/event.model';

@Component({
  selector: 'app-event-create',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PhotoUploaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-background-alt min-h-screen py-10">
      <div class="max-w-3xl mx-auto px-4 sm:px-6 lg:px-8">
        <header class="mb-8 text-center">
          <h1 class="text-3xl font-bold text-text-primary">Create a new event</h1>
          <p class="text-text-secondary mt-1">Fill in the details to publish your event.</p>
        </header>

        <!-- Progress indicator -->
        <ol class="flex items-center justify-between mb-10">
          @for (step of stepLabels; track step; let i = $index) {
            <li class="flex-1 flex items-center" [class.last]="i === stepLabels.length - 1">
              <div class="flex flex-col items-center">
                <div
                  class="w-10 h-10 rounded-full flex items-center justify-center font-semibold transition-colors"
                  [class]="currentStep() >= i ? 'bg-primary text-white' : 'bg-gray-200 text-text-secondary'"
                >
                  {{ i + 1 }}
                </div>
                <span class="hidden sm:block mt-1 text-xs font-medium text-text-secondary">{{ step }}</span>
              </div>
              @if (i < stepLabels.length - 1) {
                <div class="flex-1 h-0.5 mx-2" [class]="currentStep() > i ? 'bg-primary' : 'bg-gray-200'"></div>
              }
            </li>
          }
        </ol>

        @if (!eventCreated()) {
          <!-- STEP 1: Basic info -->
          <form [formGroup]="form" (ngSubmit)="createEvent()" class="bg-white rounded-2xl shadow-md p-6 sm:p-8">
            @if (currentStep() === 0) {
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
                  />
                  @if (invalid('name')) {
                    <p class="mt-1 text-sm text-red-600">Name is required (max 100 chars).</p>
                  }
                </div>

                <div>
                  <label for="category" class="block text-sm font-semibold text-text-primary mb-1.5">Category *</label>
                  <select
                    id="category"
                    formControlName="category"
                    class="w-full rounded-lg border border-gray-300 px-4 py-3 focus:border-primary focus:ring-primary"
                  >
                    @for (cat of categories; track cat) {
                      <option [value]="cat">{{ cat }}</option>
                    }
                  </select>
                </div>

                <div>
                  <label for="description" class="block text-sm font-semibold text-text-primary mb-1.5">Description *</label>
                  <textarea
                    id="description"
                    formControlName="description"
                    rows="5"
                    class="w-full rounded-lg border border-gray-300 px-4 py-3 focus:border-primary focus:ring-primary"
                    [class.border-red-500]="invalid('description')"
                  ></textarea>
                  @if (invalid('description')) {
                    <p class="mt-1 text-sm text-red-600">Description is required.</p>
                  }
                </div>
              </div>
            }

            <!-- STEP 2: Date & location -->
            @if (currentStep() === 1) {
              <h2 class="text-xl font-bold text-text-primary mb-4">Date &amp; location</h2>

              <div class="space-y-4">
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label for="date" class="block text-sm font-semibold text-text-primary mb-1.5">Date *</label>
                    <input
                      id="date"
                      type="date"
                      formControlName="date"
                      class="w-full rounded-lg border border-gray-300 px-4 py-3 focus:border-primary focus:ring-primary"
                      [class.border-red-500]="invalid('date')"
                    />
                    @if (invalid('date')) {
                      <p class="mt-1 text-sm text-red-600">{{ dateError() }}</p>
                    }
                  </div>
                  <div>
                    <label for="time" class="block text-sm font-semibold text-text-primary mb-1.5">Time *</label>
                    <input
                      id="time"
                      type="time"
                      formControlName="time"
                      class="w-full rounded-lg border border-gray-300 px-4 py-3 focus:border-primary focus:ring-primary"
                      [class.border-red-500]="invalid('time')"
                    />
                    @if (invalid('time')) {
                      <p class="mt-1 text-sm text-red-600">Time is required.</p>
                    }
                  </div>
                </div>

                <div>
                  <label for="street" class="block text-sm font-semibold text-text-primary mb-1.5">Street *</label>
                  <input
                    id="street"
                    type="text"
                    formControlName="street"
                    placeholder="123 Main St"
                    class="w-full rounded-lg border border-gray-300 px-4 py-3 focus:border-primary focus:ring-primary"
                    [class.border-red-500]="invalid('street')"
                  />
                  @if (invalid('street')) {
                    <p class="mt-1 text-sm text-red-600">Street is required.</p>
                  }
                </div>

                <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label for="city" class="block text-sm font-semibold text-text-primary mb-1.5">City *</label>
                    <input
                      id="city"
                      type="text"
                      formControlName="city"
                      class="w-full rounded-lg border border-gray-300 px-4 py-3 focus:border-primary focus:ring-primary"
                      [class.border-red-500]="invalid('city')"
                    />
                    @if (invalid('city')) {
                      <p class="mt-1 text-sm text-red-600">City is required.</p>
                    }
                  </div>
                  <div>
                    <label for="country" class="block text-sm font-semibold text-text-primary mb-1.5">Country *</label>
                    <input
                      id="country"
                      type="text"
                      formControlName="country"
                      class="w-full rounded-lg border border-gray-300 px-4 py-3 focus:border-primary focus:ring-primary"
                      [class.border-red-500]="invalid('country')"
                    />
                    @if (invalid('country')) {
                      <p class="mt-1 text-sm text-red-600">Country is required.</p>
                    }
                  </div>
                </div>

                <div>
                  <label for="capacity" class="block text-sm font-semibold text-text-primary mb-1.5">Capacity *</label>
                  <input
                    id="capacity"
                    type="number"
                    min="1"
                    formControlName="capacity"
                    class="w-full rounded-lg border border-gray-300 px-4 py-3 focus:border-primary focus:ring-primary"
                    [class.border-red-500]="invalid('capacity')"
                  />
                  @if (invalid('capacity')) {
                    <p class="mt-1 text-sm text-red-600">Capacity must be at least 1.</p>
                  }
                </div>
              </div>
            }

            <!-- STEP 3: Photos -->
            @if (currentStep() === 2) {
              <h2 class="text-xl font-bold text-text-primary mb-4">Photos</h2>
              <p class="text-text-secondary mb-4">Upload photos for your event. You can add more later.</p>
              @if (createdEventId) {
                <app-photo-uploader
                  [eventId]="createdEventId"
                  [photos]="uploadedPhotos()"
                  (photosChange)="uploadedPhotos.set($event)"
                />
              }
            }

            <!-- Navigation -->
            <div class="flex items-center justify-between mt-8 pt-6 border-t border-border">
              <button
                type="button"
                (click)="prev()"
                [disabled]="currentStep() === 0"
                class="rounded-full px-6 py-2.5 font-semibold text-text-secondary hover:bg-background-alt disabled:opacity-40"
              >
                ← Back
              </button>

              @if (currentStep() < totalSteps - 1) {
                <button
                  type="button"
                  (click)="next()"
                  class="rounded-full bg-primary text-white px-6 py-2.5 font-semibold hover:bg-primary-dark transition-colors"
                >
                  Next →
                </button>
              } @else {
                <button
                  type="submit"
                  [disabled]="submitting()"
                  class="rounded-full bg-primary text-white px-6 py-2.5 font-semibold hover:bg-primary-dark transition-colors disabled:opacity-60"
                >
                  {{ submitting() ? 'Creating…' : 'Create Event' }}
                </button>
              }
            </div>
          </form>
        } @else {
          <!-- STEP 2: Add ticket types -->
          <div class="bg-white rounded-2xl shadow-md p-6 sm:p-8">
            <h2 class="text-xl font-bold text-text-primary mb-4">Add ticket types</h2>
            <p class="text-text-secondary mb-4">Define ticket tiers for your event. Attendees will pick from these at checkout.</p>

            <div class="space-y-4 mb-6">
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
                    <input type="text" formControlName="currency" value="USD" class="w-full rounded-lg border border-gray-300 px-3 py-2 focus:border-primary focus:ring-primary" />
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
                  (click)="skipTicketTypes()"
                  class="rounded-full px-6 py-2.5 font-semibold text-text-secondary hover:bg-background-alt"
                >
                  Skip — add later
                </button>
                <button
                  type="button"
                  (click)="submitTicketTypes()"
                  [disabled]="addingTickets()"
                  class="rounded-full bg-primary text-white px-6 py-2.5 font-semibold hover:bg-primary-dark transition-colors disabled:opacity-60"
                >
                  {{ addingTickets() ? 'Saving…' : 'Save Ticket Types' }}
                </button>
              </div>
            }
          </div>
        }
      </div>
    </div>
  `,
})
export class EventCreateComponent {
  private fb = inject(UntypedFormBuilder);
  private eventApp = inject(EventApplicationService);
  private toast = inject(ToastService);
  private router = inject(Router);

  protected readonly stepLabels = ['Details', 'Date & Location', 'Photos'];
  protected readonly totalSteps = this.stepLabels.length;

  readonly currentStep = signal(0);
  readonly submitting = signal(false);
  readonly addingTickets = signal(false);
  readonly eventCreated = signal(false);
  readonly uploadedPhotos = signal<EventPhotoResponse[]>([]);

  protected createdEventId = '';

  readonly currencies = ['USD', 'EUR', 'GBP'];

  protected readonly categories = EVENT_CATEGORIES;

  readonly form: UntypedFormGroup = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    category: ['Music', [Validators.required]],
    description: ['', [Validators.required]],
    date: ['', [Validators.required]],
    time: ['', [Validators.required]],
    street: ['', [Validators.required]],
    city: ['', [Validators.required]],
    country: ['', [Validators.required]],
    capacity: [50, [Validators.required, Validators.min(1)]],
  });

  ticketTypesForm: UntypedFormGroup = this.fb.group({
    tickets: this.fb.array([]),
  });

  get ticketTypes(): UntypedFormArray {
    return this.ticketTypesForm.get('tickets') as UntypedFormArray;
  }

  next(): void {
    if (this.isStepValid(this.currentStep())) {
      this.currentStep.update((s) => Math.min(this.totalSteps - 1, s + 1));
    } else {
      this.markStepTouched(this.currentStep());
      this.toast.showError('Please complete the required fields before continuing.');
    }
  }

  prev(): void {
    this.currentStep.update((s) => Math.max(0, s - 1));
  }

  private isStepValid(step: number): boolean {
    switch (step) {
      case 0:
        return !!(this.form.get('name')?.valid) && !!(this.form.get('description')?.valid);
      case 1:
        return !!(this.form.get('date')?.valid) && !!(this.form.get('time')?.valid)
          && !!(this.form.get('street')?.valid) && !!(this.form.get('city')?.valid)
          && !!(this.form.get('country')?.valid) && !!(this.form.get('capacity')?.valid)
          && this.isFutureDate();
      case 2:
        return true; // photos are optional
      default:
        return true;
    }
  }

  private markStepTouched(step: number): void {
    const fields = step === 0
      ? ['name', 'description']
      : ['date', 'time', 'street', 'city', 'country', 'capacity'];
    if (step === 2) return; // no form fields for photos step
    fields.forEach((f) => this.form.get(f)?.markAsTouched());
  }

  addTicket(): void {
    this.ticketTypes.push(
      this.fb.group({
        name: ['', [Validators.required]],
        amount: [0, [Validators.required, Validators.min(0)]],
        currency: ['USD', [Validators.required]],
        capacity: [1, [Validators.required, Validators.min(1)]],
      }),
    );
  }

  removeTicket(index: number): void {
    this.ticketTypes.removeAt(index);
  }

  invalid(field: string): boolean {
    const c = this.form.get(field);
    return !!c && c.invalid && (c.touched || c.dirty);
  }

  dateError(): string {
    const c = this.form.get('date');
    if (c?.errors?.['required']) {
      return 'Date is required.';
    }
    if (!this.isFutureDate()) {
      return 'Date must be in the future.';
    }
    return 'Invalid date.';
  }

  private isFutureDate(): boolean {
    const value = this.form.get('date')?.value;
    if (!value) {
      return false;
    }
    const chosen = new Date(value).getTime();
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return chosen >= today.getTime();
  }

  createEvent(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.showError('Please fix the errors before submitting.');
      return;
    }
    this.submitting.set(true);

    const { name, description, date, time, street, city, country, capacity, category } = this.form.value;
    const combinedDate = new Date(`${date}T${time}`).toISOString();

    this.eventApp
      .createEvent({
        name,
        capacity,
        date: combinedDate,
        location: { country, city, street },
        description,
        type: category,
      })
      .subscribe({
        next: (result) => {
          this.submitting.set(false);
          if (result.isSuccess && result.value) {
            this.createdEventId = result.value;
            this.toast.showSuccess('Event created! Now add ticket types.');
            this.eventCreated.set(true);
          } else if (result.isFailure) {
            this.toast.showError(result.errors?.[0]?.message ?? 'Could not create the event.');
          }
        },
        error: () => this.submitting.set(false),
      });
  }

  submitTicketTypes(): void {
    const tickets = this.ticketTypes.value as AddTicketTypeRequest[];
    if (tickets.length === 0) {
      this.toast.showSuccess('Event created successfully!');
      this.router.navigateByUrl('/dashboard/organizer');
      return;
    }

    this.addingTickets.set(true);
    let completed = 0;
    let hasError = false;

    for (const ticket of tickets) {
      this.eventApp.addTicketType(this.createdEventId, ticket).subscribe({
        next: (result) => {
          completed++;
          if (result.isFailure) {
            hasError = true;
            this.toast.showError(result.errors?.[0]?.message ?? 'Failed to add a ticket type.');
          }
          if (completed === tickets.length && !hasError) {
            this.toast.showSuccess('Event and ticket types created successfully!');
            this.router.navigateByUrl('/dashboard/organizer');
          }
        },
        error: () => {
          completed++;
          if (completed === tickets.length) {
            this.router.navigateByUrl('/dashboard/organizer');
          }
        },
      });
    }
  }

  skipTicketTypes(): void {
    this.toast.showSuccess('Event created successfully!');
    this.router.navigateByUrl('/dashboard/organizer');
  }
}