import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { EventPhotoHttpService } from '../../../application/http/event-photo.http-service';
import { ToastService } from '../../../infrastructure/services/toast.service';
import { EventPhotoResponse } from '../../../core/models/event.model';

interface UploadingFile {
  file: File;
  previewUrl: string;
  progress: number;
}

@Component({
  selector: 'app-photo-uploader',
  standalone: true,
  imports: [CommonModule, DragDropModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!-- Drop zone -->
    <div
      class="border-2 border-dashed rounded-xl p-8 text-center cursor-pointer transition-colors"
      [class.border-primary]="dragOver()"
      [class.border-gray-300]="!dragOver()"
      (dragover)="onDragOver($event)"
      (dragleave)="dragOver.set(false)"
      (drop)="onDrop($event)"
      (click)="fileInput.click()"
    >
      <input
        #fileInput
        type="file"
        multiple
        accept="image/jpeg,image/png,image/webp"
        class="hidden"
        (change)="onFileSelected($event)"
      />
      <p class="text-text-secondary mb-1">Drop photos here or click to browse</p>
      <p class="text-xs text-text-secondary">JPEG, PNG, WebP up to 5MB each (max 10 per batch)</p>
    </div>

    <!-- Uploading progress -->
    @for (uf of uploadingFiles(); track uf.file.name) {
      <div class="flex items-center gap-3 mt-3 p-3 bg-background-alt rounded-lg">
        <img [src]="uf.previewUrl" class="w-14 h-14 rounded object-cover" alt="" />
        <div class="flex-1 min-w-0">
          <p class="text-sm font-medium text-text-primary truncate">{{ uf.file.name }}</p>
          <div class="h-1.5 w-full bg-gray-200 rounded-full mt-1">
            <div class="h-full bg-primary rounded-full transition-all" [style.width.%]="uf.progress"></div>
          </div>
        </div>
      </div>
    }

    <!-- Uploaded photos grid (CDK DragDrop) -->
    @if (photos().length > 0) {
      <div
        cdkDropList
        (cdkDropListDropped)="onReorder($event)"
        class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3 mt-4"
      >
        @for (photo of photos(); track photo.id; let i = $index) {
          <div
            cdkDrag
            class="relative group rounded-xl overflow-hidden border border-border bg-white"
          >
            <img
              [src]="photo.publicUrl"
              [alt]="photo.caption ?? 'Photo'"
              class="w-full aspect-square object-cover"
              loading="lazy"
            />

            <!-- Overlay on hover -->
            <div class="absolute inset-0 bg-black/50 opacity-0 group-hover:opacity-100 transition-opacity flex flex-col items-center justify-center gap-2 p-2">
              @if (!photo.isCover) {
                <button
                  (click)="setCover(photo.id)"
                  class="text-xs bg-white/90 text-text-primary px-3 py-1 rounded-full font-semibold hover:bg-white"
                >Set as Cover</button>
              } @else {
                <span class="text-xs bg-primary text-white px-3 py-1 rounded-full font-semibold">Cover</span>
              }
              <button
                (click)="deletePhoto(photo.id)"
                class="text-xs bg-red-500/90 text-white px-3 py-1 rounded-full font-semibold hover:bg-red-500"
              >Delete</button>
            </div>

            <!-- Caption input -->
            <div class="p-2">
              <input
                [value]="photo.caption ?? ''"
                (blur)="updateCaption(photo.id, $event)"
                placeholder="Add caption…"
                class="w-full text-xs border-0 p-0 focus:ring-0 outline-none text-text-secondary placeholder-text-secondary"
              />
            </div>

            <!-- Drag handle -->
            <div class="absolute top-1 right-1 text-white/70 text-lg cursor-grab active:cursor-grabbing" cdkDragHandle>&#9776;</div>
          </div>
        }
      </div>
    }
  `,
})
export class PhotoUploaderComponent {
  private destroyRef = inject(DestroyRef);
  private photoService = inject(EventPhotoHttpService);
  private toast = inject(ToastService);

  readonly eventId = input.required<string>();
  readonly photos = input<EventPhotoResponse[]>([]);
  readonly photosChange = output<EventPhotoResponse[]>();

  readonly dragOver = signal(false);
  readonly uploadingFiles = signal<UploadingFile[]>([]);

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.dragOver.set(true);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.dragOver.set(false);
    const files = Array.from(event.dataTransfer?.files ?? []);
    if (files.length > 0) this.upload(files);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.upload(Array.from(input.files));
    }
    input.value = '';
  }

  private upload(files: File[]): void {
    if (files.length > 10) {
      this.toast.showError('Maximum 10 files per upload.');
      return;
    }
    const invalid = files.find(f => !['image/jpeg', 'image/png', 'image/webp'].includes(f.type));
    if (invalid) {
      this.toast.showError(`${invalid.name} is not a supported image format.`);
      return;
    }
    const oversized = files.find(f => f.size > 5 * 1024 * 1024);
    if (oversized) {
      this.toast.showError(`${oversized.name} exceeds the 5MB limit.`);
      return;
    }

    const previews: UploadingFile[] = files.map(f => ({
      file: f,
      previewUrl: URL.createObjectURL(f),
      progress: 0,
    }));
    this.uploadingFiles.set(previews);

    this.photoService.uploadPhotos(this.eventId(), files).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        this.uploadingFiles.set([]);
        previews.forEach(p => URL.revokeObjectURL(p.previewUrl));
        if (result.isSuccess && result.value) {
          this.photosChange.emit(result.value);
          this.toast.showSuccess(`${result.value.length} photo(s) uploaded.`);
        } else {
          this.toast.showError(result.errors?.[0]?.message ?? 'Upload failed.');
        }
      },
      error: () => {
        this.uploadingFiles.set([]);
        previews.forEach(p => URL.revokeObjectURL(p.previewUrl));
        this.toast.showError('Upload failed.');
      },
    });
  }

  setCover(photoId: string): void {
    this.photoService.setCoverPhoto(this.eventId(), photoId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        if (result.isSuccess) {
          const updated = this.photos().map(p => ({ ...p, isCover: p.id === photoId }));
          this.photosChange.emit(updated);
          this.toast.showSuccess('Cover photo updated.');
        } else {
          this.toast.showError(result.errors?.[0]?.message ?? 'Failed to set cover.');
        }
      },
    });
  }

  deletePhoto(photoId: string): void {
    this.photoService.deletePhoto(this.eventId(), photoId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        if (result.isSuccess) {
          const updated = this.photos().filter(p => p.id !== photoId);
          this.photosChange.emit(updated);
          this.toast.showSuccess('Photo deleted.');
        } else {
          this.toast.showError(result.errors?.[0]?.message ?? 'Failed to delete photo.');
        }
      },
    });
  }

  updateCaption(photoId: string, event: Event): void {
    const caption = (event.target as HTMLInputElement).value;
    this.photoService.updatePhotoMetadata(this.eventId(), photoId, { caption, displayOrder: null }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        if (result.isSuccess) {
          const updated = this.photos().map(p => p.id === photoId ? { ...p, caption } : p);
          this.photosChange.emit(updated);
        }
      },
    });
  }

  onReorder(event: CdkDragDrop<EventPhotoResponse[]>): void {
    const ordered = [...this.photos()];
    moveItemInArray(ordered, event.previousIndex, event.currentIndex);
    this.photosChange.emit(ordered);

    this.photoService.reorderPhotos(this.eventId(), ordered.map(p => p.id)).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      error: () => this.toast.showError('Failed to save new order.'),
    });
  }
}
