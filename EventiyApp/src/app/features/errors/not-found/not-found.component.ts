import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-[70vh] flex flex-col items-center justify-center text-center px-4">
      <p class="text-7xl font-extrabold text-primary mb-4">404</p>
      <h1 class="text-3xl font-bold text-text-primary mb-2">Page not found</h1>
      <p class="text-text-secondary mb-8 max-w-md">
        The page you're looking for doesn't exist or has been moved.
      </p>
      <a
        routerLink="/"
        class="rounded-full bg-primary text-white px-6 py-3 font-semibold hover:bg-primary-dark transition-colors"
      >
        Back to home
      </a>
    </div>
  `,
})
export class NotFoundComponent {}
