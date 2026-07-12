import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, UntypedFormArray, UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthApplicationService } from '../../../application/services/auth-application.service';
import { AdminEventHttpService } from '../../../application/http/admin-event.http-service';
import { EventHttpService } from '../../../application/http/event.http-service';
import { EventApplicationService } from '../../../application/services/event-application.service';
import { ToastService } from '../../../infrastructure/services/toast.service';
import { PhotoUploaderComponent } from '../../../shared/components/photo-uploader/photo-uploader.component';
import { AddTicketTypeRequest, EventPhotoResponse, EVENT_CATEGORIES } from '../../../core/models/event.model';

const VENUE_SECTIONS: Record<string, string[]> = {
  Music: ['FP1', 'FP2', 'MF1', 'MF2', 'SB1', 'SB2', 'REAR', 'VIP1', 'VIP2'],
  Sports: ['116', '124L', '234', 'S105', 'S118', 'S180', 'C129', 'GC19', 'VVIP1', 'VVIP2'],
  Theater: ['ORCH', 'ORCHL', 'ORCHR', 'MEZZ', 'BALC', 'BOXL', 'BOXR', 'FRONT'],
};

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

        @if (!eventCreated()) {
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

            @if (currentStep() === 2) {
              <h2 class="text-xl font-bold text-text-primary mb-4">Ticket types</h2>
              <p class="text-text-secondary mb-4">Define ticket tiers for your event. Attendees will pick from these at checkout.</p>

              <div [formGroup]="ticketTypesForm">
                <div formArrayName="tickets" class="space-y-4 mb-6">
                  @for (ticket of ticketTypes.controls; track $index; let i = $index) {
                    <div [formGroupName]="i" class="grid grid-cols-1 sm:grid-cols-[1fr_120px_100px_120px_120px_auto] gap-3 items-end bg-background-alt rounded-xl p-4">
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
                      @if (hasVenueSections()) {
                        <div>
                          <label class="block text-xs font-semibold text-text-primary mb-1">Section</label>
                          <select formControlName="sectionCode" class="w-full rounded-lg border border-gray-300 px-3 py-2.5 focus:border-primary focus:ring-primary">
                            @for (code of categorySections(); track code) {
                              <option [value]="code">{{ code }}</option>
                            }
                          </select>
                        </div>
                      }
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
              </div>
            }

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
          <div class="bg-white rounded-2xl shadow-md p-6 sm:p-8 text-center">
            <p class="text-5xl mb-3" aria-hidden="true">✅</p>
            <h3 class="text-xl font-semibold text-text-primary mb-2">Event created!</h3>
            <p class="text-text-secondary mb-6">You can now upload photos or go to your dashboard.</p>

            @if (createdEventId) {
              <div class="mb-6 text-left">
                <app-photo-uploader
                  [eventId]="createdEventId"
                  [photos]="uploadedPhotos()"
                  (photosChange)="uploadedPhotos.set($event)"
                />
              </div>
            }

            <div class="flex items-center justify-center gap-4">
              <button
                type="button"
                (click)="router.navigateByUrl('/events/' + createdEventId)"
                class="rounded-full bg-primary text-white px-6 py-2.5 font-semibold hover:bg-primary-dark transition-colors"
              >
                View Event
              </button>
              <button
                type="button"
                (click)="router.navigateByUrl('/dashboard/organizer')"
                class="rounded-full border-2 border-primary text-primary px-6 py-2.5 font-semibold hover:bg-primary hover:text-white transition-colors"
              >
                Go to Dashboard
              </button>
            </div>
          </div>
        }
      </div>
    </div>
  `,
})
export class EventCreateComponent {
  protected router = inject(Router);
  private destroyRef = inject(DestroyRef);
  private fb = inject(UntypedFormBuilder);
  private eventApp = inject(EventApplicationService);
  private adminHttp = inject(AdminEventHttpService);
  private eventHttp = inject(EventHttpService);
  private auth = inject(AuthApplicationService);
  private toast = inject(ToastService);

  get isAdmin(): boolean {
    return this.auth.userRole() === 'Admin';
  }

  protected readonly stepLabels = ['Details', 'Date & Location', 'Ticket Types'];
  protected readonly totalSteps = this.stepLabels.length;

  readonly currentStep = signal(0);
  readonly submitting = signal(false);
  readonly eventCreated = signal(false);
  readonly uploadedPhotos = signal<EventPhotoResponse[]>([]);

  protected createdEventId = '';

  protected readonly categories = EVENT_CATEGORIES;

  /** Valid venue sections for the currently selected event category. */
  readonly categorySections = computed(() => {
    const cat = this.form.get('category')?.value ?? '';
    return VENUE_SECTIONS[cat] ?? null;
  });

  readonly hasVenueSections = computed(() => this.categorySections() !== null);

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
        return true;
      default:
        return true;
    }
  }

  private markStepTouched(step: number): void {
    const fields = step === 0
      ? ['name', 'description']
      : ['date', 'time', 'street', 'city', 'country', 'capacity'];
    fields.forEach((f) => this.form.get(f)?.markAsTouched());
  }

  addTicket(): void {
    const sections = this.categorySections();
    this.ticketTypes.push(
      this.fb.group({
        name: ['', [Validators.required]],
        amount: [10, [Validators.required, Validators.min(1)]],
        currency: ['USD', [Validators.required]],
        capacity: [1, [Validators.required, Validators.min(1)]],
        sectionCode: [sections?.[0] ?? ''],
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
    const tickets = this.ticketTypes.value as AddTicketTypeRequest[];

    this.eventApp
      .createEvent({
        name,
        capacity,
        date: combinedDate,
        location: { country, city, street },
        description,
        type: category,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          if (result.isSuccess && result.value) {
            this.createdEventId = result.value;
            if (tickets.length === 0) {
              this.submitting.set(false);
              this.eventCreated.set(true);
              this.toast.showSuccess('Event created successfully!');
              return;
            }
            this.addTicketsToEvent(tickets);
          } else if (result.isFailure) {
            this.submitting.set(false);
            this.toast.showError(result.errors?.[0]?.message ?? 'Could not create the event.');
          }
        },
        error: () => {
          this.submitting.set(false);
          this.toast.showError('Could not reach the server.');
        },
      });
  }

  private addTicketsToEvent(tickets: AddTicketTypeRequest[]): void {
    const addFn = this.isAdmin
      ? (t: AddTicketTypeRequest) => this.adminHttp.addTicketType(this.createdEventId, t)
      : (t: AddTicketTypeRequest) => this.eventHttp.addTicketType(this.createdEventId, t);

    let completed = 0;
    let hasError = false;

    for (const ticket of tickets) {
      addFn(ticket).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: (res) => {
          completed++;
          if (res.isFailure) {
            hasError = true;
            this.toast.showError(res.errors?.[0]?.message ?? 'Failed to add a ticket type.');
          }
          if (completed === tickets.length) {
            this.submitting.set(false);
            this.eventCreated.set(true);
            if (hasError) {
              this.toast.showError('Event created but some ticket types failed. You can add them later.');
            }
          }
        },
        error: () => {
          completed++;
          if (completed === tickets.length) {
            this.submitting.set(false);
            this.eventCreated.set(true);
          }
        },
      });
    }
  }
}
