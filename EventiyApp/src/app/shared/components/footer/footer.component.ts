import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

/**
 * Meetup-inspired site footer.
 *
 * Multi-column link layout with brand info, event categories, company links,
 * and social icons. Collapses to stacked layout on mobile.
 */
@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [CommonModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="footer">
      <div class="footer-inner">
        <!-- Brand column -->
        <div class="footer-brand">
          <img src="logo.png" alt="Eventiy" class="footer-logo">
          <p class="footer-tagline">
            The people platform — Where interests become friendships.
          </p>
        </div>

        <!-- Discover column -->
        <div class="footer-column">
          <h4 class="footer-heading">Discover</h4>
          <nav class="footer-links">
            <a routerLink="/events" class="footer-link">Browse Events</a>
            <a routerLink="/events?type=Music" class="footer-link">Music</a>
            <a routerLink="/events?type=Tech" class="footer-link">Tech</a>
            <a routerLink="/events?type=Sports" class="footer-link">Sports</a>
            <a routerLink="/events?type=Art" class="footer-link">Art</a>
          </nav>
        </div>

        <!-- Company column -->
        <div class="footer-column">
          <h4 class="footer-heading">Company</h4>
          <nav class="footer-links">
            <a href="#" class="footer-link">About</a>
            <a href="#" class="footer-link">Careers</a>
            <a href="#" class="footer-link">Press</a>
            <a href="#" class="footer-link">Contact</a>
          </nav>
        </div>

        <!-- Support column -->
        <div class="footer-column">
          <h4 class="footer-heading">Support</h4>
          <nav class="footer-links">
            <a href="#" class="footer-link">Help Center</a>
            <a href="#" class="footer-link">Terms of Service</a>
            <a href="#" class="footer-link">Privacy Policy</a>
            <a href="#" class="footer-link">Cookie Policy</a>
          </nav>
        </div>
      </div>

      <!-- Bottom bar -->
      <div class="footer-bottom">
        <p class="footer-copyright">
          &copy; {{ currentYear }} Eventiy. All rights reserved.
        </p>
        <div class="footer-social">
          <!-- Twitter/X -->
          <a href="#" class="footer-social-icon" aria-label="Twitter">
            <svg fill="currentColor" viewBox="0 0 24 24"><path d="M18.244 2.25h3.308l-7.227 8.26 8.502 11.24H16.17l-5.214-6.817L4.99 21.75H1.68l7.73-8.835L1.254 2.25H8.08l4.713 6.231zm-1.161 17.52h1.833L7.084 4.126H5.117z"/></svg>
          </a>
          <!-- Instagram -->
          <a href="#" class="footer-social-icon" aria-label="Instagram">
            <svg fill="currentColor" viewBox="0 0 24 24"><path d="M12 2.163c3.204 0 3.584.012 4.85.07 3.252.148 4.771 1.691 4.919 4.919.058 1.265.069 1.645.069 4.849 0 3.205-.012 3.584-.069 4.849-.149 3.225-1.664 4.771-4.919 4.919-1.266.058-1.644.07-4.85.07-3.204 0-3.584-.012-4.849-.07-3.26-.149-4.771-1.699-4.919-4.92-.058-1.265-.07-1.644-.07-4.849 0-3.204.013-3.583.07-4.849.149-3.227 1.664-4.771 4.919-4.919 1.266-.057 1.645-.069 4.849-.069zM12 0C8.741 0 8.333.014 7.053.072 2.695.272.273 2.69.073 7.052.014 8.333 0 8.741 0 12c0 3.259.014 3.668.072 4.948.2 4.358 2.618 6.78 6.98 6.98C8.333 23.986 8.741 24 12 24c3.259 0 3.668-.014 4.948-.072 4.354-.2 6.782-2.618 6.979-6.98.059-1.28.073-1.689.073-4.948 0-3.259-.014-3.667-.072-4.947-.196-4.354-2.617-6.78-6.979-6.98C15.668.014 15.259 0 12 0zm0 5.838a6.162 6.162 0 100 12.324 6.162 6.162 0 000-12.324zM12 16a4 4 0 110-8 4 4 0 010 8zm6.406-11.845a1.44 1.44 0 100 2.881 1.44 1.44 0 000-2.881z"/></svg>
          </a>
          <!-- Facebook -->
          <a href="#" class="footer-social-icon" aria-label="Facebook">
            <svg fill="currentColor" viewBox="0 0 24 24"><path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z"/></svg>
          </a>
        </div>
      </div>
    </footer>
  `,
  styles: [`
    /* ── Footer ─────────────────────────────────────── */
    .footer {
      background: #1F2937;
      color: #D1D5DB;
      padding: 3.5rem 2rem 1.5rem;
      margin-top: 2rem;
    }

    .footer-inner {
      display: grid;
      grid-template-columns: 1.5fr 1fr 1fr 1fr;
      gap: 3rem;
      max-width: 1280px;
      margin: 0 auto;
    }

    /* ── Brand ────────────────────────────────────────── */
    .footer-brand {
      max-width: 280px;
    }

    .footer-logo {
      height: 2rem;
      width: auto;
      filter: brightness(0) invert(1);
      opacity: 0.9;
    }

    .footer-tagline {
      margin-top: 1rem;
      font-size: 0.875rem;
      line-height: 1.6;
      color: #9CA3AF;
    }

    /* ── Columns ──────────────────────────────────────── */
    .footer-column {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .footer-heading {
      font-size: 0.875rem;
      font-weight: 700;
      color: #ffffff;
      margin: 0;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .footer-links {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .footer-link {
      font-size: 0.875rem;
      color: #9CA3AF;
      text-decoration: none;
      transition: color 0.2s;
    }

    .footer-link:hover {
      color: #ffffff;
    }

    /* ── Bottom Bar ──────────────────────────────────── */
    .footer-bottom {
      display: flex;
      align-items: center;
      justify-content: space-between;
      max-width: 1280px;
      margin: 3rem auto 0;
      padding-top: 1.5rem;
      border-top: 1px solid #374151;
    }

    .footer-copyright {
      font-size: 0.8125rem;
      color: #6B7280;
      margin: 0;
    }

    .footer-social {
      display: flex;
      gap: 1rem;
    }

    .footer-social-icon {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 2rem;
      height: 2rem;
      color: #9CA3AF;
      border-radius: 0.5rem;
      transition: all 0.2s;
      text-decoration: none;
    }

    .footer-social-icon:hover {
      color: #ffffff;
      background: rgba(255, 255, 255, 0.1);
    }

    .footer-social-icon svg {
      width: 1.125rem;
      height: 1.125rem;
    }

    /* ── Responsive ──────────────────────────────────── */
    @media (max-width: 768px) {
      .footer {
        padding: 2.5rem 1.25rem 1.25rem;
      }

      .footer-inner {
        grid-template-columns: 1fr 1fr;
        gap: 2rem;
      }

      .footer-brand {
        grid-column: 1 / -1;
      }

      .footer-bottom {
        flex-direction: column;
        gap: 1rem;
        text-align: center;
      }
    }

    @media (max-width: 480px) {
      .footer-inner {
        grid-template-columns: 1fr;
        gap: 1.5rem;
      }
    }
  `],
})
export class FooterComponent {
  get currentYear(): number {
    return new Date().getFullYear();
  }
}
