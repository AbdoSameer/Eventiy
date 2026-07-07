import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthApplicationService } from '../../../application/services/auth-application.service';
import { ToastService } from '../../../infrastructure/services/toast.service';

/**
 * Login page.
 *
 * Reactive form with inline validation; on success, redirects based on role:
 *   Attendee → /
 *   Organizer/Admin → /dashboard/organizer
 * Honors an optional `returnUrl` query param set by authGuard.
 */
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-[calc(100vh-4rem)] flex items-center justify-center bg-background-alt px-4 py-12">
      <div class="w-full max-w-md">
        <div class="bg-white rounded-2xl shadow-md p-8">
          <div class="text-center mb-8">
            <h1 class="text-3xl font-bold text-text-primary">Welcome back</h1>
            <p class="text-text-secondary mt-2">Log in to your Eventiy account</p>
          </div>

          <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-5" novalidate>
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
                autocomplete="current-password"
                class="w-full rounded-lg border border-gray-300 px-4 py-3 text-text-primary focus:border-primary focus:ring-primary"
                [class.border-red-500]="invalid('password')"
                placeholder="••••••••"
              />
              @if (invalid('password')) {
                <p class="mt-1.5 text-sm text-red-600">{{ errorMessage('password') }}</p>
              }
            </div>

            <!-- Remember me -->
            <div class="flex items-center gap-2">
              <input id="remember" type="checkbox" formControlName="remember" class="rounded border-gray-300 text-primary focus:ring-primary" />
              <label for="remember" class="text-sm text-text-secondary">Remember me</label>
            </div>

            <button
              type="submit"
              [disabled]="loading()"
              class="w-full rounded-full bg-primary text-white px-6 py-3 font-semibold hover:bg-primary-dark transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
            >
              {{ loading() ? 'Logging in…' : 'Log in' }}
            </button>
          </form>

          <p class="text-center text-sm text-text-secondary mt-6">
            Don't have an account?
            <a routerLink="/register" class="text-primary font-semibold hover:underline ml-1">Sign up</a>
          </p>
        </div>
      </div>
    </div>
  `,
})
export class LoginComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private fb = inject(UntypedFormBuilder);
  private auth = inject(AuthApplicationService);
  private router = inject(Router);
  private toast = inject(ToastService);

  readonly loading = signal(false);
  readonly form: UntypedFormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]],
    remember: [false],
  });

  ngOnInit(): void {
    // If already logged in, skip the form.
    if (this.auth.isAuthenticated()) {
      this.redirectByRole(this.auth.userRole());
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    const { email, password } = this.form.value;

    this.auth.login({ email, password }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        this.loading.set(false);
        if (result.isSuccess && result.value) {
          if (result.value.requiresApproval || !result.value.token) {
            this.toast.showInfo('Your account is pending admin approval. Please try again later.');
            return;
          }
          this.toast.showSuccess('Welcome back!');
          const returnUrl = this.getReturnUrl();
          if (returnUrl) {
            this.router.navigateByUrl(returnUrl);
          } else {
            this.redirectByRole(result.value.role);
          }
        } else if (result.isFailure) {
          this.toast.showError(result.errors?.[0]?.message ?? 'Login failed.');
        }
      },
      error: () => this.loading.set(false),
    });
  }

  // --- form helpers ------------------------------------------------------

  invalid(field: string): boolean {
    const c = this.form.get(field);
    return !!c && c.invalid && (c.touched || c.dirty);
  }

  errorMessage(field: string): string {
    const c = this.form.get(field);
    if (!c || !c.errors) {
      return '';
    }
    if (c.errors['required']) {
      return field === 'email' ? 'Email is required.' : 'Password is required.';
    }
    if (c.errors['email']) {
      return 'Enter a valid email address.';
    }
    if (c.errors['minlength']) {
      return 'Password must be at least 6 characters.';
    }
    return 'Invalid value.';
  }

  private redirectByRole(role: string | null): void {
    if (role === 'Organizer' || role === 'Admin') {
      this.router.navigateByUrl('/dashboard/organizer');
    } else {
      this.router.navigateByUrl('/');
    }
  }

  private getReturnUrl(): string | null {
    const params = new URLSearchParams(window.location.search);
    return params.get('returnUrl');
  }
}
