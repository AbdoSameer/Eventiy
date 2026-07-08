import { ChangeDetectionStrategy, Component, EventEmitter, Output } from '@angular/core';
import { SearchBarComponent, SearchCriteria } from '../../../shared/components/search-bar/search-bar.component';

/**
 * Meetup-inspired hero section.
 *
 * Two-column layout on desktop: left text + search, right illustration.
 * On mobile it stacks vertically. Emits `search` from the embedded SearchBar.
 */
@Component({
  selector: 'app-hero',
  standalone: true,
  imports: [SearchBarComponent],
  templateUrl: './hero.component.html',
  styleUrls: ['./hero.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HeroComponent {
  @Output() search = new EventEmitter<SearchCriteria>();

  onSearch(criteria: SearchCriteria): void {
    this.search.emit(criteria);
  }
}
