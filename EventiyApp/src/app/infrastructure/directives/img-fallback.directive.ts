import { Directive, HostListener } from '@angular/core';

@Directive({
  selector: 'img[appImgFallback]',
  standalone: true,
})
export class ImgFallbackDirective {
  @HostListener('error', ['$event'])
  onError(event: Event): void {
    const el = event.target as HTMLImageElement;
    if (el) el.style.display = 'none';
  }
}
