import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { NavbarComponent } from './shared/components/navbar/navbar.component';
import { FooterComponent } from './shared/components/footer/footer.component';
import { ResultToastComponent } from './shared/components/result-toast/result-toast.component';

/**
 * Root application shell.
 *
 * Holds the persistent Navbar (auth-aware), Footer, and the global ResultToast host.
 * RouterOutlet renders the lazy-loaded feature components defined in app.routes.ts.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, NavbarComponent, FooterComponent, ResultToastComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-navbar />
    <main class="min-h-screen">
      <router-outlet />
    </main>
    <app-footer />
    <app-result-toast />
  `,
})
export class AppComponent {}
