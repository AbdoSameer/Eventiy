import { ChangeDetectionStrategy, Component, HostListener, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthApplicationService } from '../../../application/services/auth-application.service';

/**
 * Sticky, responsive navigation bar.
 *
 * - Auth-aware right side: anonymous users see Log in / Sign up; authenticated
 *   users see an avatar dropdown + mobile hamburger drawer.
 * - Active route highlighting via RouterLinkActive.
 * - Click-outside closes the dropdown/drawer (HostListener).
 */
@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="sticky top-0 z-50 bg-white/80 backdrop-blur-md border-b border-border">
      <nav class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex items-center justify-between h-16">
          <!-- Logo -->
          <a routerLink="/" class="flex items-center gap-2">
            <img src="logo.png" alt="Eventiy" class="h-9 w-auto">
          </a>

          <!-- Desktop links -->
          <div class="hidden md:flex items-center gap-6">
            <a
              routerLink="/events"
              routerLinkActive="text-primary font-semibold"
              class="text-text-secondary hover:text-primary transition-colors"
            >
              Browse Events
            </a>

            <!-- Authenticated -->
            <ng-container *ngIf="auth.isAuthenticated(); else anonymous">
              <!-- Avatar dropdown -->
              <div class="relative">
                <button
                  (click)="toggleMenu()"
                  class="flex items-center gap-2 rounded-full p-1 pr-3 hover:bg-background-alt transition-colors"
                  [attr.aria-expanded]="menuOpen()"
                  aria-label="Open user menu"
                >
                  <span class="w-9 h-9 rounded-full bg-primary text-white flex items-center justify-center font-semibold">
                    {{ initials }}
                  </span>
                  <span class="hidden lg:inline text-sm font-medium text-text-primary">
                    {{ auth.currentUser()?.email }}
                  </span>
                  <svg class="w-4 h-4 text-text-secondary" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
                  </svg>
                </button>

                <!-- Dropdown -->
                <div
                  *ngIf="menuOpen()"
                  class="absolute right-0 mt-2 w-56 bg-white rounded-2xl shadow-xl border border-border py-2 animate-slide-in"
                >
                  <div class="px-4 py-2 border-b border-border">
                    <p class="text-sm font-semibold text-text-primary truncate">{{ auth.currentUser()?.email }}</p>
                    <p class="text-xs text-text-secondary">{{ auth.userRole() }}</p>
                  </div>

                  <a
                    *ngIf="auth.userRole() === 'Attendee'"
                    routerLink="/dashboard/attendee"
                    (click)="closeMenu()"
                    class="block px-4 py-2 text-sm text-text-primary hover:bg-background-alt transition-colors"
                  >
                    My Bookings
                  </a>
                  <a
                    *ngIf="auth.userRole() === 'Organizer' || auth.userRole() === 'Admin'"
                    routerLink="/dashboard/organizer"
                    (click)="closeMenu()"
                    class="block px-4 py-2 text-sm text-text-primary hover:bg-background-alt transition-colors"
                  >
                    Organizer Dashboard
                  </a>
                  <a
                    routerLink="/events"
                    (click)="closeMenu()"
                    class="block px-4 py-2 text-sm text-text-primary hover:bg-background-alt transition-colors"
                  >
                    Browse Events
                  </a>
                  <button
                    (click)="logout()"
                    class="w-full text-left px-4 py-2 text-sm text-primary-dark hover:bg-background-alt transition-colors"
                  >
                    Log out
                  </button>
                </div>
              </div>
            </ng-container>

            <!-- Anonymous -->
            <ng-template #anonymous>
              <a
                routerLink="/login"
                class="rounded-full px-6 py-2.5 font-semibold border-2 border-primary text-primary hover:bg-primary hover:text-white transition-colors"
              >
                Log in
              </a>
              <a
                routerLink="/register"
                class="rounded-full px-6 py-2.5 font-semibold bg-primary text-white hover:bg-primary-dark transition-colors"
              >
                Sign up
              </a>
            </ng-template>
          </div>

          <!-- Mobile hamburger -->
          <button
            class="md:hidden p-2 rounded-lg hover:bg-background-alt transition-colors"
            (click)="toggleDrawer()"
            [attr.aria-expanded]="drawerOpen()"
            aria-label="Toggle navigation menu"
          >
            <svg class="w-6 h-6 text-text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
              <path *ngIf="!drawerOpen()" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16" />
              <path *ngIf="drawerOpen()" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
      </nav>

      <!-- Mobile drawer -->
      <div *ngIf="drawerOpen()" class="md:hidden border-t border-border bg-white">
        <div class="px-4 py-4 space-y-2">
          <a
            routerLink="/events"
            routerLinkActive="text-primary font-semibold"
            (click)="closeDrawer()"
            class="block py-2 text-text-primary hover:text-primary transition-colors"
          >
            Browse Events
          </a>

          <ng-container *ngIf="auth.isAuthenticated(); else anonymousMobile">
            <a
              *ngIf="auth.userRole() === 'Attendee'"
              routerLink="/dashboard/attendee"
              (click)="closeDrawer()"
              class="block py-2 text-text-primary hover:text-primary transition-colors"
            >
              My Bookings
            </a>
            <a
              *ngIf="auth.userRole() === 'Organizer' || auth.userRole() === 'Admin'"
              routerLink="/dashboard/organizer"
              (click)="closeDrawer()"
              class="block py-2 text-text-primary hover:text-primary transition-colors"
            >
              Organizer Dashboard
            </a>
            <button
              (click)="logout()"
              class="w-full text-left py-2 text-primary-dark font-semibold"
            >
              Log out
            </button>
          </ng-container>

          <ng-template #anonymousMobile>
            <a
              routerLink="/login"
              (click)="closeDrawer()"
              class="block py-2 rounded-full text-center font-semibold border-2 border-primary text-primary"
            >
              Log in
            </a>
            <a
              routerLink="/register"
              (click)="closeDrawer()"
              class="block py-2 rounded-full text-center font-semibold bg-primary text-white"
            >
              Sign up
            </a>
          </ng-template>
        </div>
      </div>
    </header>
  `,
})
export class NavbarComponent {
  auth = inject(AuthApplicationService);
  private router = inject(Router);

  menuOpen = signal(false);
  drawerOpen = signal(false);

  get initials(): string {
    const email = this.auth.currentUser()?.email ?? '';
    return email.charAt(0).toUpperCase() || 'U';
  }

  toggleMenu(): void {
    this.menuOpen.update((v) => !v);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  toggleDrawer(): void {
    this.drawerOpen.update((v) => !v);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  logout(): void {
    this.closeMenu();
    this.closeDrawer();
    this.auth.logout();
  }

  /** Close any open menu when clicking outside the navbar. */
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('header')) {
      this.menuOpen.set(false);
    }
  }
}
