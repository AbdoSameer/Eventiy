import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthApplicationService } from '../../../application/services/auth-application.service';
import { ToastService } from '../../../infrastructure/services/toast.service';
import { UserRole } from '../../../core/models/auth.model';

/**
 * Registration page.
 *
 * Validates password strength (min 8 chars, 1 uppercase, 1 number), shows a
 * live strength bar, and on success auto-logs the user in (the register
 * endpoint returns an AuthResponse) then redirects by role.
 */
@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-[calc(100vh-4rem)] flex items-center justify-center bg-background-alt px-4 py-12">
      <div class="w-full max-w-lg">
        <div class="bg-white rounded-2xl shadow-md p-8">
          <div class="text-center mb-8">
            <h1 class="text-3xl font-bold text-text-primary">Create your account</h1>
            <p class="text-text-secondary mt-2">Join Eventiy and start booking events</p>
          </div>

          <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-5" novalidate>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <!-- First name -->
              <div>
                <label for="firstName" class="block text-sm font-semibold text-text-primary mb-1.5">First name</label>
                <input
                  id="firstName"
                  type="text"
                  formControlName="firstName"
                  autocomplete="given-name"
                  class="w-full rounded-lg border border-gray-300 px-4 py-3 text-text-primary focus:border-primary focus:ring-primary"
                  [class.border-red-500]="invalid('firstName')"
                />
                @if (invalid('firstName')) {
                  <p class="mt-1.5 text-sm text-red-600">First name is required.</p>
                }
              </div>

              <!-- Last name -->
              <div>
                <label for="lastName" class="block text-sm font-semibold text-text-primary mb-1.5">Last name</label>
                <input
                  id="lastName"
                  type="text"
                  formControlName="lastName"
                  autocomplete="family-name"
                  class="w-full rounded-lg border border-gray-300 px-4 py-3 text-text-primary focus:border-primary focus:ring-primary"
                  [class.border-red-500]="invalid('lastName')"
                />
                @if (invalid('lastName')) {
                  <p class="mt-1.5 text-sm text-red-600">Last name is required.</p>
                }
              </div>
            </div>

            <!-- Email -->
            <div>
              <label for="email" class="block text-sm font-semibold text-text-primary mb-1.5">Email</label>
              <input
                id="email"
                type="email"
                formControlName="email"
                autocomplete="email"
                class="w-full rounded-lg border border-gray-300 px-4 py-3 text-text-primary focus:border-primary focus:ring-primary"
                [class.border-red-500]="invalid('email')"
                placeholder="you@example.com"
              />
              @if (invalid('email')) {
                <p class="mt-1.5 text-sm text-red-600">{{ errorMessage('email') }}</p>
              }
            </div>

            <!-- Password -->
            <div>
              <label for="password" class="block text-sm font-semibold text-text-primary mb-1.5">Password</label>
              <input
                id="password"
                type="password"
                formControlName="password"
                autocomplete="new-password"
                class="w-full rounded-lg border border-gray-300 px-4 py-3 text-text-primary focus:border-primary focus:ring-primary"
                [class.border-red-500]="invalid('password')"
                placeholder="At least 8 characters, 1 uppercase, 1 number"
                (input)="onPasswordInput()"
              />
              <!-- Strength bar -->
              <div class="mt-2 flex gap-1">
                @for (s of strengthSegments; track s) {
                  <div
                    class="h-1.5 flex-1 rounded-full transition-colors"
                    [class]="strength() >= s ? strengthColor : 'bg-gray-200'"
                  ></div>
                }
              </div>
              <p class="mt-1 text-xs text-text-secondary">
                Strength: <span class="font-semibold">{{ strengthLabel() }}</span>
              </p>
              @if (invalid('password')) {
                <p class="mt-1.5 text-sm text-red-600">{{ errorMessage('password') }}</p>
              }
            </div>

            <!-- Confirm password -->
            <div>
              <label for="confirmPassword" class="block text-sm font-semibold text-text-primary mb-1.5">Confirm password</label>
              <input
                id="confirmPassword"
                type="password"
                formControlName="confirmPassword"
                autocomplete="new-password"
                class="w-full rounded-lg border border-gray-300 px-4 py-3 text-text-primary focus:border-primary focus:ring-primary"
                [class.border-red-500]="invalid('confirmPassword')"
                placeholder="Re-enter your password"
              />
              @if (invalid('confirmPassword')) {
                <p class="mt-1.5 text-sm text-red-600">Passwords do not match.</p>
              }
            </div>

            <!-- Role selector -->
            <fieldset>
              <legend class="block text-sm font-semibold text-text-primary mb-2">I want to…</legend>
              <div class="grid grid-cols-2 gap-3">
                <label
                  class="flex items-center gap-3 rounded-xl border-2 p-4 cursor-pointer transition-colors"
                  [class]="form.value.role === 'Attendee' ? 'border-primary bg-primary/5' : 'border-gray-200'"
                >
                  <input type="radio" formControlName="role" value="Attendee" class="text-primary focus:ring-primary" />
                  <span>
                    <span class="block font-semibold text-text-primary">Attend events</span>
                    <span class="block text-xs text-text-secondary">Book tickets</span>
                  </span>
                </label>
                <label
                  class="flex items-center gap-3 rounded-xl border-2 p-4 cursor-pointer transition-colors"
                  [class]="form.value.role === 'Organizer' ? 'border-primary bg-primary/5' : 'border-gray-200'"
                >
                  <input type="radio" formControlName="role" value="Organizer" class="text-primary focus:ring-primary" />
                  <span>
                    <span class="block font-semibold text-text-primary">Host events</span>
                    <span class="block text-xs text-text-secondary">Organizer access</span>
                  </span>
                </label>
              </div>
            </fieldset>

            <button
              type="submit"
              [disabled]="loading()"
              class="w-full rounded-full bg-primary text-white px-6 py-3 font-semibold hover:bg-primary-dark transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
            >
              {{ loading() ? 'Creating account…' : 'Create account' }}
            </button>
          </form>

          <p class="text-center text-sm text-text-secondary mt-6">
            Already have an account?
            <a routerLink="/login" class="text-primary font-semibold hover:underline ml-1">Log in</a>
          </p>
        </div>
      </div>
    </div>
  `,
})
export class RegisterComponent implements OnInit {
  private fb = inject(UntypedFormBuilder);
  private auth = inject(AuthApplicationService);
  private router = inject(Router);
  private toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(false);
  readonly strength = signal(0); // 0..4

  protected readonly strengthSegments = [1, 2, 3, 4];

  readonly form: UntypedFormGroup = this.fb.group(
    {
      firstName: ['', [Validators.required]],
      lastName: ['', [Validators.required]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]],
      role: ['Attendee', [Validators.required]],
    },
    { validators: this.passwordMatchValidator() },
  );

  ngOnInit(): void {
    if (this.auth.isAuthenticated()) {
      this.router.navigateByUrl('/');
    }
  }

  /** Cross-field validator: password === confirmPassword. */
  private passwordMatchValidator(): (group: UntypedFormGroup) => { [key: string]: boolean } | null {
    return (group: UntypedFormGroup) => {
      const pw = group.get('password')?.value;
      const cpw = group.get('confirmPassword')?.value;
      if (pw && cpw && pw !== cpw) {
        return { passwordMismatch: true };
      }
      return null;
    };
  }

  onPasswordInput(): void {
    this.updateStrength();
  }

  private updateStrength(): void {
    const pw: string = this.form.get('password')?.value ?? '';
    let score = 0;
    if (pw.length >= 8) score++;
    if (/[A-Z]/.test(pw)) score++;
    if (/[0-9]/.test(pw)) score++;
    if (/[^A-Za-z0-9]/.test(pw)) score++;
    this.strength.set(score);
  }

  strengthLabel(): string {
    switch (this.strength()) {
      case 0:
      case 1:
        return 'Weak';
      case 2:
        return 'Fair';
      case 3:
        return 'Good';
      default:
        return 'Strong';
    }
  }

  get strengthColor(): string {
    switch (this.strength()) {
      case 0:
      case 1:
        return 'bg-red-500';
      case 2:
        return 'bg-yellow-500';
      case 3:
        return 'bg-blue-500';
      default:
        return 'bg-green-500';
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    const { firstName, lastName, email, password, role } = this.form.value;

    this.auth.register({ firstName, lastName, email, password, role: role as UserRole })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
      next: (result) => {
        this.loading.set(false);
        if (result.isSuccess && result.value) {
          if (result.value.requiresApproval) {
            this.toast.showSuccess('Account created! An admin will review your organizer request. You will be able to log in once approved.');
            this.router.navigateByUrl('/');
          } else {
            this.toast.showSuccess('Account created. Welcome to Eventiy!');
            this.redirectByRole(result.value.role);
          }
        } else if (result.isFailure) {
          this.toast.showError(result.errors?.[0]?.message ?? 'Registration failed.');
        }
      },
      error: () => this.loading.set(false),
    });
  }

  // --- form helpers ------------------------------------------------------

  invalid(field: string): boolean {
    const c = this.form.get(field);
    if (field === 'confirmPassword') {
      return !!this.form.errors?.['passwordMismatch'] && !!c?.touched && !!c.value;
    }
    return !!c && c.invalid && (c.touched || c.dirty);
  }

  errorMessage(field: string): string {
    const c = this.form.get(field);
    if (!c || !c.errors) {
      return '';
    }
    if (c.errors['required']) {
      return `${field === 'email' ? 'Email' : 'Password'} is required.`;
    }
    if (c.errors['email']) {
      return 'Enter a valid email address.';
    }
    if (c.errors['minlength']) {
      return 'Password must be at least 8 characters.';
    }
    return 'Invalid value.';
  }

  private redirectByRole(role: UserRole): void {
    if (role === 'Organizer' || role === 'Admin') {
      this.router.navigateByUrl('/dashboard/organizer');
    } else {
      this.router.navigateByUrl('/');
    }
  }
}
