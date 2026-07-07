import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from '../../../infrastructure/services/toast.service';

/**
 * Global toast host rendered once in AppComponent.
 *
 * Reads the toast queue from ToastService and renders each item in the
 * top-right corner. Each toast auto-dismisses after 4 seconds.
 */
@Component({
  selector: 'app-result-toast',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="fixed top-4 right-4 z-[100] flex flex-col gap-3 max-w-sm w-full pointer-events-none">
      @for (toast of toastService.toasts(); track toast.id) {
        <div
          class="pointer-events-auto rounded-2xl shadow-xl px-5 py-4 flex items-start gap-3 animate-slide-in"
          [class]="bgClass(toast.type)"
        >
          <span class="text-xl shrink-0" aria-hidden="true">{{ icon(toast.type) }}</span>
          <p class="text-sm font-medium flex-1">{{ toast.message }}</p>
          <button
            (click)="toastService.dismiss(toast.id)"
            class="shrink-0 opacity-80 hover:opacity-100"
            aria-label="Dismiss notification"
          >
            ✕
          </button>
          <!-- Auto-dismiss on a delay -->
          {{ autoDismiss(toast.id) }}
        </div>
      }
    </div>
  `,
})
export class ResultToastComponent {
  protected toastService = inject(ToastService);
  private timers = new Map<number, ReturnType<typeof setTimeout>>();

  protected bgClass(type: string): string {
    switch (type) {
      case 'success':
        return 'bg-green-600 text-white';
      case 'error':
        return 'bg-red-600 text-white';
      default:
        return 'bg-blue-600 text-white';
    }
  }

  protected icon(type: string): string {
    switch (type) {
      case 'success':
        return '✅';
      case 'error':
        return '⚠️';
      default:
        return 'ℹ️';
    }
  }

  /**
   * Schedules auto-dismissal 4s after the toast first renders.
   * Returned value is empty string so the interpolation renders nothing.
   */
  protected autoDismiss(id: number): string {
    if (this.timers.has(id)) {
      return '';
    }
    this.timers.set(
      id,
      setTimeout(() => {
        this.toastService.dismiss(id);
        this.timers.delete(id);
      }, 4000),
    );
    return '';
  }
}
