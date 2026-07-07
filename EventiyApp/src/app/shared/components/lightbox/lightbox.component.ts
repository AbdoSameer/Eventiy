import { ChangeDetectionStrategy, Component, HostListener, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EventPhotoResponse } from '../../../core/models/event.model';
import { ImgFallbackDirective } from '../../../infrastructure/directives/img-fallback.directive';

@Component({
  selector: 'app-lightbox',
  standalone: true,
  imports: [CommonModule, ImgFallbackDirective],
  templateUrl: './lightbox.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LightboxComponent {
  readonly photos = input<EventPhotoResponse[]>([]);
  readonly close = output<void>();

  readonly currentIndex = signal(0);

  @HostListener('document:keydown', ['$event'])
  handleKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.close.emit();
    } else if (event.key === 'ArrowLeft') {
      this.goPrev();
    } else if (event.key === 'ArrowRight') {
      this.goNext();
    }
  }

  protected goPrev(): void {
    const len = this.photos().length;
    if (len < 2) return;
    this.currentIndex.update(i => ((i - 1) % len + len) % len);
  }

  protected goNext(): void {
    const len = this.photos().length;
    if (len < 2) return;
    this.currentIndex.update(i => (i + 1) % len);
  }

}
