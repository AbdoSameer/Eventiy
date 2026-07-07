import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthApplicationService } from '../../../../application/services/auth-application.service';

/** Shown when a roleGuard rejects navigation. */
@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-[70vh] flex flex-col items-center justify-center text-center px-4">
      <p class="text-6xl mb-4" aria-hidden="true">🔒</p>
      <h1 class="text-3xl font-bold text-text-primary mb-2">Access denied</h1>
      <p class="text-text-secondary mb-8 max-w-md">
        You don't have permission to view this page.
        @if (auth.isAuthenticated()) {
          Your role ({{ auth.userRole() }}) doesn't allow access.
        } @else {
          Please log in with an authorized account.
        }
      </p>
      <div class="flex gap-3">
        <a
          routerLink="/"
          class="rounded-full bg-primary text-white px-6 py-3 font-semibold hover:bg-primary-dark transition-colors"
        >
          Back to home
        </a>
        @if (!auth.isAuthenticated()) {
          <a
            routerLink="/login"
            class="rounded-full border-2 border-primary text-primary px-6 py-3 font-semibold hover:bg-primary hover:text-white transition-colors"
          >
            Log in
          </a>
        }
      </div>
    </div>
  `,
})
export class UnauthorizedComponent {
  protected auth = inject(AuthApplicationService);
}
