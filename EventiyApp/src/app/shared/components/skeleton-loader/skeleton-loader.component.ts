import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SkeletonType = 'card' | 'list' | 'detail';

/**
 * Pulse-animated skeleton used while data loads (no spinners).
 *
 * - `card`   → grid of placeholders matching EventCardComponent
 * - `list`   → stacked rows for tables / dashboard lists
 * - `detail` → large hero + body blocks for the detail page
 */
@Component({
  selector: 'app-skeleton-loader',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @switch (type) {
      @case ('card') {
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          @for (i of placeholders(6); track i) {
            <div class="bg-white rounded-2xl shadow-md overflow-hidden animate-pulse">
              <div class="aspect-video bg-gray-200"></div>
              <div class="p-5 space-y-3">
                <div class="h-5 bg-gray-200 rounded w-3/4"></div>
                <div class="h-4 bg-gray-200 rounded w-1/2"></div>
                <div class="h-4 bg-gray-200 rounded w-2/3"></div>
                <div class="h-9 bg-gray-200 rounded-full w-1/3 mt-2"></div>
              </div>
            </div>
          }
        </div>
      }
      @case ('list') {
        <div class="space-y-4">
          @for (i of placeholders(5); track i) {
            <div class="bg-white rounded-2xl shadow-md p-5 flex items-center gap-4 animate-pulse">
              <div class="w-12 h-12 rounded-full bg-gray-200 shrink-0"></div>
              <div class="flex-1 space-y-2">
                <div class="h-4 bg-gray-200 rounded w-1/3"></div>
                <div class="h-3 bg-gray-200 rounded w-1/4"></div>
              </div>
              <div class="h-8 bg-gray-200 rounded-full w-20"></div>
            </div>
          }
        </div>
      }
      @case ('detail') {
        <div class="space-y-6 animate-pulse">
          <div class="w-full h-96 bg-gray-200 rounded-2xl"></div>
          <div class="max-w-4xl mx-auto space-y-4 px-4">
            <div class="h-8 bg-gray-200 rounded w-2/3"></div>
            <div class="h-4 bg-gray-200 rounded w-1/3"></div>
            <div class="h-24 bg-gray-200 rounded"></div>
            <div class="h-24 bg-gray-200 rounded"></div>
          </div>
        </div>
      }
    }
  `,
})
export class SkeletonLoaderComponent {
  @Input() type: SkeletonType = 'card';

  /** Helper to iterate N times in the template. */
  protected placeholders(count: number): number[] {
    return Array.from({ length: count }, (_, i) => i);
  }
}
