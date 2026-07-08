import { ChangeDetectionStrategy, Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface SearchCriteria {
  keyword: string;
  location: string;
}

/**
 * Clean, full-width search bar with keyword + location inputs.
 *
 * Meetup-inspired design: white card, icon-prefixed inputs, rounded CTA.
 * Emits a `search` event with the keyword + location.
 */
@Component({
  selector: 'app-search-bar',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="search-bar-wrapper">
      <form (ngSubmit)="onSubmit()" class="search-form">
        <!-- Keyword input -->
        <div class="search-field">
          <input
            type="text"
            name="keyword"
            [(ngModel)]="keyword"
            placeholder="Search events"
            class="search-input"
            aria-label="Search events"
          />
        </div>

        <div class="search-divider"></div>

        <!-- Location input -->
        <div class="search-field">
          <input
            type="text"
            name="location"
            [(ngModel)]="location"
            placeholder="Location"
            class="search-input"
            aria-label="Location"
          />
        </div>

        <button type="submit" class="search-btn" aria-label="Search">
          <svg class="search-btn-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </button>
      </form>
    </div>
  `,
  styles: [`
    .search-bar-wrapper {
      background-color: #ffffff;
      border-radius: 9999px; /* pill shape */
      box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04);
      border: 1px solid #E5E7EB;
      padding: 0.5rem;
      width: 100%;
    }

    .search-form {
      display: flex;
      align-items: center;
      width: 100%;
    }

    .search-field {
      flex: 1;
      display: flex;
      align-items: center;
      padding: 0 1rem;
    }

    .search-input {
      width: 100%;
      border: none;
      outline: none;
      font-size: 1rem;
      color: #1F2937;
      background: transparent;
      font-family: inherit;
      padding: 0.5rem 0;
    }

    .search-input::placeholder {
      color: #9CA3AF;
      font-weight: 500;
    }

    .search-divider {
      width: 1px;
      height: 2.5rem;
      background-color: #E5E7EB;
      flex-shrink: 0;
    }

    .search-btn {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 3rem;
      height: 3rem;
      background-color: #F6544C;
      color: #ffffff;
      border: none;
      border-radius: 9999px; /* perfect circle */
      cursor: pointer;
      transition: background-color 0.2s, transform 0.15s;
      flex-shrink: 0;
      margin-left: 0.5rem;
    }

    .search-btn:hover {
      background-color: #E53E3E;
    }

    .search-btn:active {
      transform: scale(0.95);
    }

    .search-btn-icon {
      width: 1.25rem;
      height: 1.25rem;
    }

    /* Responsive */
    @media (max-width: 640px) {
      .search-bar-wrapper {
        border-radius: 1.5rem;
      }
      
      .search-form {
        flex-direction: column;
      }

      .search-divider {
        width: calc(100% - 2rem);
        height: 1px;
        margin: 0.5rem 1rem;
      }

      .search-field {
        width: 100%;
        padding: 0.5rem 1rem;
      }

      .search-btn {
        width: calc(100% - 1rem);
        border-radius: 1rem;
        margin: 0.5rem;
        height: 3rem;
      }
    }
  `]
})
export class SearchBarComponent {
  @Output() search = new EventEmitter<SearchCriteria>();

  keyword = '';
  location = '';

  onSubmit(): void {
    this.search.emit({
      keyword: this.keyword.trim(),
      location: this.location.trim(),
    });
  }
}
