import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AdminOutboxHttpService, DeadLetterDto } from '../../../application/http/admin-outbox.http-service';
import { ToastService } from '../../../infrastructure/services/toast.service';

@Component({
  selector: 'app-admin-outbox',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-6xl px-4 py-8">
      <h1 class="text-2xl font-bold text-gray-900 mb-6">Dead-Letter Queue</h1>

      @if (loading()) {
        <p class="text-gray-500">Loading...</p>
      } @else if (deadLetters().length === 0) {
        <p class="text-gray-500">No dead-lettered messages.</p>
      } @else {
        <div class="overflow-x-auto rounded-lg border border-gray-200">
          <table class="min-w-full divide-y divide-gray-200">
            <thead class="bg-gray-50">
              <tr>
                <th class="px-4 py-3 text-left text-xs font-semibold text-gray-600 uppercase">Event Name</th>
                <th class="px-4 py-3 text-left text-xs font-semibold text-gray-600 uppercase">Domain</th>
                <th class="px-4 py-3 text-left text-xs font-semibold text-gray-600 uppercase">Retries</th>
                <th class="px-4 py-3 text-left text-xs font-semibold text-gray-600 uppercase">Failed Reason</th>
                <th class="px-4 py-3 text-left text-xs font-semibold text-gray-600 uppercase">Moved At</th>
                <th class="px-4 py-3 text-right text-xs font-semibold text-gray-600 uppercase">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-200">
              @for (dl of deadLetters(); track dl.id) {
                <tr class="hover:bg-gray-50">
                  <td class="px-4 py-3 text-sm text-gray-900">{{ dl.eventName }}</td>
                  <td class="px-4 py-3 text-sm text-gray-600">{{ dl.domain }}</td>
                  <td class="px-4 py-3 text-sm text-gray-600">{{ dl.retryCount }}</td>
                  <td class="px-4 py-3 text-sm text-red-600 max-w-xs truncate" [title]="dl.failedReason">{{ dl.failedReason }}</td>
                  <td class="px-4 py-3 text-sm text-gray-500">{{ dl.movedToDeadLetterAt | date: 'short' }}</td>
                  <td class="px-4 py-3 text-right">
                    <button
                      type="button"
                      class="rounded-md bg-indigo-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-indigo-700 disabled:opacity-50"
                      [disabled]="requeuingId() === dl.id"
                      (click)="requeue(dl)"
                    >
                      @if (requeuingId() === dl.id) {
                        Requeuing...
                      } @else {
                        Requeue
                      }
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
})
export class AdminOutboxComponent {
  private readonly outboxHttp = inject(AdminOutboxHttpService);
  private readonly toast = inject(ToastService);

  readonly deadLetters = signal<DeadLetterDto[]>([]);
  readonly loading = signal(true);
  readonly requeuingId = signal<string | null>(null);

  constructor() {
    this.loadDeadLetters();
  }

  private loadDeadLetters(): void {
    this.loading.set(true);
    this.outboxHttp.getDeadLetters().pipe(takeUntilDestroyed()).subscribe({
      next: (result) => {
        this.loading.set(false);
        if (result.isSuccess && result.value) {
          this.deadLetters.set(result.value);
        } else {
          this.toast.showError(result.errors?.[0]?.message ?? 'Failed to load dead letters.');
        }
      },
      error: () => {
        this.loading.set(false);
        this.toast.showError('Could not reach the server.');
      },
    });
  }

  requeue(dl: DeadLetterDto): void {
    this.requeuingId.set(dl.id);
    this.outboxHttp.requeueDeadLetter(dl.id).pipe(takeUntilDestroyed()).subscribe({
      next: (result) => {
        this.requeuingId.set(null);
        if (result.isSuccess) {
          this.toast.showSuccess(`Requeued "${dl.eventName}" for processing.`);
          this.deadLetters.update((list) => list.filter((d) => d.id !== dl.id));
        } else {
          this.toast.showError(result.errors?.[0]?.message ?? 'Requeue failed.');
        }
      },
      error: () => {
        this.requeuingId.set(null);
        this.toast.showError('Could not reach the server.');
      },
    });
  }
}
