import { ChangeDetectionStrategy, Component, HostListener, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthApplicationService } from '../../../application/services/auth-application.service';

/**
 * Meetup-inspired sticky navigation bar.
 *
 * - Clean white header with subtle bottom border and backdrop blur
 * - Auth-aware right side: anonymous → Log in / Sign up; authenticated → avatar dropdown
 * - Mobile hamburger drawer
 * - Click-outside closes menus
 */
@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="navbar">
      <nav class="navbar-inner">
        <!-- Logo -->
        <a routerLink="/" class="navbar-logo">
          <img src="logo.png" alt="Eventiy" class="navbar-logo-img">
        </a>

        <!-- Desktop links -->
        <div class="navbar-desktop">
          <a
            routerLink="/events"
            routerLinkActive="navbar-link-active"
            class="navbar-link"
          >
            Browse Events
          </a>

          <!-- Authenticated -->
          <ng-container *ngIf="auth.isAuthenticated(); else anonymous">
            <div class="navbar-user">
              <button
                (click)="toggleMenu()"
                class="navbar-avatar-btn"
                [attr.aria-expanded]="menuOpen()"
                aria-label="Open user menu"
              >
                <span class="navbar-avatar">{{ initials }}</span>
                <span class="navbar-user-name">{{ auth.currentUser()?.email }}</span>
                <svg class="navbar-chevron" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
                </svg>
              </button>

              <!-- Dropdown -->
              <div *ngIf="menuOpen()" class="navbar-dropdown">
                <div class="navbar-dropdown-header">
                  <p class="navbar-dropdown-email">{{ auth.currentUser()?.email }}</p>
                  <p class="navbar-dropdown-role">{{ auth.userRole() }}</p>
                </div>
                <div class="navbar-dropdown-links">
                  <a *ngIf="auth.userRole() === 'Attendee'" routerLink="/dashboard/attendee" (click)="closeMenu()" class="navbar-dropdown-item">
                    My Bookings
                  </a>
                  <a *ngIf="auth.userRole() === 'Organizer' || auth.userRole() === 'Admin'" routerLink="/dashboard/organizer" (click)="closeMenu()" class="navbar-dropdown-item">
                    Organizer Dashboard
                  </a>
                  <a routerLink="/events" (click)="closeMenu()" class="navbar-dropdown-item">
                    Browse Events
                  </a>
                </div>
                <div class="navbar-dropdown-footer">
                  <button (click)="logout()" class="navbar-dropdown-item navbar-dropdown-logout">
                    Log out
                  </button>
                </div>
              </div>
            </div>
          </ng-container>

          <!-- Anonymous -->
          <ng-template #anonymous>
            <a routerLink="/login" class="navbar-btn-outline">
              Log in
            </a>
            <a routerLink="/register" class="navbar-btn-primary">
              Sign up
            </a>
          </ng-template>
        </div>

        <!-- Mobile hamburger -->
        <button
          class="navbar-hamburger"
          (click)="toggleDrawer()"
          [attr.aria-expanded]="drawerOpen()"
          aria-label="Toggle navigation menu"
        >
          <svg class="navbar-hamburger-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
            <path *ngIf="!drawerOpen()" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16" />
            <path *ngIf="drawerOpen()" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </nav>

      <!-- Mobile drawer -->
      <div *ngIf="drawerOpen()" class="navbar-drawer">
        <div class="navbar-drawer-inner">
          <a routerLink="/events" routerLinkActive="navbar-link-active" (click)="closeDrawer()" class="navbar-drawer-link">
            Browse Events
          </a>

          <ng-container *ngIf="auth.isAuthenticated(); else anonymousMobile">
            <a *ngIf="auth.userRole() === 'Attendee'" routerLink="/dashboard/attendee" (click)="closeDrawer()" class="navbar-drawer-link">
              My Bookings
            </a>
            <a *ngIf="auth.userRole() === 'Organizer' || auth.userRole() === 'Admin'" routerLink="/dashboard/organizer" (click)="closeDrawer()" class="navbar-drawer-link">
              Organizer Dashboard
            </a>
            <button (click)="logout()" class="navbar-drawer-link navbar-drawer-logout">
              Log out
            </button>
          </ng-container>

          <ng-template #anonymousMobile>
            <a routerLink="/login" (click)="closeDrawer()" class="navbar-drawer-btn-outline">
              Log in
            </a>
            <a routerLink="/register" (click)="closeDrawer()" class="navbar-drawer-btn-primary">
              Sign up
            </a>
          </ng-template>
        </div>
      </div>
    </header>
  `,
  styles: [`
    /* ── Navbar ──────────────────────────────────────── */
    .navbar {
      position: sticky;
      top: 0;
      z-index: 50;
      background: rgba(255, 255, 255, 0.92);
      backdrop-filter: blur(12px);
      -webkit-backdrop-filter: blur(12px);
      border-bottom: 1px solid #F3F4F6;
    }

    .navbar-inner {
      display: flex;
      align-items: center;
      justify-content: space-between;
      max-width: 1280px;
      margin: 0 auto;
      padding: 0 2rem;
      height: 4rem;
    }

    /* ── Logo ─────────────────────────────────────────── */
    .navbar-logo {
      display: flex;
      align-items: center;
      text-decoration: none;
    }

    .navbar-logo-img {
      height: 2.25rem;
      width: auto;
    }

    /* ── Desktop Links ──────────────────────────────── */
    .navbar-desktop {
      display: flex;
      align-items: center;
      gap: 2rem;
    }

    .navbar-link {
      font-size: 0.9375rem;
      font-weight: 500;
      color: #4B5563;
      text-decoration: none;
      transition: color 0.2s;
    }

    .navbar-link:hover,
    .navbar-link-active {
      color: #F6544C;
      font-weight: 600;
    }

    /* ── Auth Buttons (Desktop) ──────────────────────── */
    .navbar-btn-outline {
      display: inline-flex;
      align-items: center;
      padding: 0.5rem 1.25rem;
      border-radius: 9999px;
      border: 1.5px solid #F6544C;
      color: #F6544C;
      font-size: 0.9375rem;
      font-weight: 600;
      text-decoration: none;
      transition: all 0.2s;
    }

    .navbar-btn-outline:hover {
      background: #F6544C;
      color: #ffffff;
    }

    .navbar-btn-primary {
      display: inline-flex;
      align-items: center;
      padding: 0.5rem 1.25rem;
      border-radius: 9999px;
      background: #F6544C;
      color: #ffffff;
      font-size: 0.9375rem;
      font-weight: 600;
      text-decoration: none;
      transition: background 0.2s;
    }

    .navbar-btn-primary:hover {
      background: #E53E3E;
    }

    /* ── User Avatar & Dropdown ──────────────────────── */
    .navbar-user {
      position: relative;
    }

    .navbar-avatar-btn {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.25rem 0.5rem 0.25rem 0.25rem;
      border-radius: 9999px;
      border: none;
      background: transparent;
      cursor: pointer;
      transition: background 0.2s;
      font-family: inherit;
    }

    .navbar-avatar-btn:hover {
      background: #F9FAFB;
    }

    .navbar-avatar {
      width: 2.25rem;
      height: 2.25rem;
      border-radius: 9999px;
      background: #F6544C;
      color: #ffffff;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 700;
      font-size: 0.875rem;
    }

    .navbar-user-name {
      font-size: 0.875rem;
      font-weight: 500;
      color: #1F2937;
    }

    .navbar-chevron {
      width: 1rem;
      height: 1rem;
      color: #9CA3AF;
    }

    .navbar-dropdown {
      position: absolute;
      right: 0;
      top: calc(100% + 0.5rem);
      width: 14rem;
      background: #ffffff;
      border-radius: 1rem;
      box-shadow: 0 20px 50px rgba(0, 0, 0, 0.12);
      border: 1px solid #E5E7EB;
      overflow: hidden;
      animation: dropdownIn 0.15s ease-out;
    }

    @keyframes dropdownIn {
      from { opacity: 0; transform: translateY(-4px); }
      to { opacity: 1; transform: translateY(0); }
    }

    .navbar-dropdown-header {
      padding: 0.875rem 1rem;
      border-bottom: 1px solid #F3F4F6;
    }

    .navbar-dropdown-email {
      font-size: 0.875rem;
      font-weight: 600;
      color: #1F2937;
      margin: 0 0 0.125rem 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .navbar-dropdown-role {
      font-size: 0.75rem;
      color: #9CA3AF;
      margin: 0;
      text-transform: capitalize;
    }

    .navbar-dropdown-links {
      padding: 0.5rem 0;
    }

    .navbar-dropdown-item {
      display: block;
      padding: 0.625rem 1rem;
      font-size: 0.875rem;
      color: #374151;
      text-decoration: none;
      cursor: pointer;
      transition: background 0.15s;
      border: none;
      background: none;
      width: 100%;
      text-align: left;
      font-family: inherit;
    }

    .navbar-dropdown-item:hover {
      background: #F9FAFB;
    }

    .navbar-dropdown-footer {
      border-top: 1px solid #F3F4F6;
      padding: 0.5rem 0;
    }

    .navbar-dropdown-logout {
      color: #E53E3E;
    }

    /* ── Mobile Hamburger ────────────────────────────── */
    .navbar-hamburger {
      display: none;
      padding: 0.5rem;
      border: none;
      background: none;
      cursor: pointer;
      border-radius: 0.5rem;
      transition: background 0.2s;
      color: #1F2937;
    }

    .navbar-hamburger:hover {
      background: #F9FAFB;
    }

    .navbar-hamburger-icon {
      width: 1.5rem;
      height: 1.5rem;
    }

    /* ── Mobile Drawer ───────────────────────────────── */
    .navbar-drawer {
      display: none;
      border-top: 1px solid #F3F4F6;
      background: #ffffff;
    }

    .navbar-drawer-inner {
      padding: 1rem 2rem 1.5rem;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }

    .navbar-drawer-link {
      display: block;
      padding: 0.75rem 0;
      font-size: 1rem;
      font-weight: 500;
      color: #374151;
      text-decoration: none;
      border: none;
      background: none;
      text-align: left;
      cursor: pointer;
      font-family: inherit;
      transition: color 0.2s;
    }

    .navbar-drawer-link:hover,
    .navbar-drawer-link-active {
      color: #F6544C;
    }

    .navbar-drawer-logout {
      color: #E53E3E;
      font-weight: 600;
      border-top: 1px solid #F3F4F6;
      margin-top: 0.5rem;
      padding-top: 1rem;
    }

    .navbar-drawer-btn-outline {
      display: block;
      text-align: center;
      padding: 0.75rem;
      border-radius: 9999px;
      border: 1.5px solid #F6544C;
      color: #F6544C;
      font-weight: 600;
      text-decoration: none;
      margin-top: 0.5rem;
    }

    .navbar-drawer-btn-primary {
      display: block;
      text-align: center;
      padding: 0.75rem;
      border-radius: 9999px;
      background: #F6544C;
      color: #ffffff;
      font-weight: 600;
      text-decoration: none;
      margin-top: 0.5rem;
    }

    /* ── Responsive ──────────────────────────────────── */
    @media (max-width: 768px) {
      .navbar-inner {
        padding: 0 1rem;
      }

      .navbar-desktop {
        display: none;
      }

      .navbar-hamburger {
        display: flex;
      }

      .navbar-drawer {
        display: block;
      }

      .navbar-drawer-inner {
        padding: 0.75rem 1rem 1.25rem;
      }
    }
  `],
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
