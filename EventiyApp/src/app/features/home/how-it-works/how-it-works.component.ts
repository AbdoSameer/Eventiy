import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-how-it-works',
  standalone: true,
  templateUrl: './how-it-works.component.html',
  styleUrls: ['./how-it-works.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HowItWorksComponent {
}
