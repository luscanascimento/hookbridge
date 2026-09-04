import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-skeleton-loader',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="animate-pulse bg-surface-800/60 rounded" [ngClass]="customClass()"></div>
  `
})
export class SkeletonLoaderComponent {
  readonly customClass = input<string>('h-4 w-full');
}
