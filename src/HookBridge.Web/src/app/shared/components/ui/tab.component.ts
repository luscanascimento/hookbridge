import { Component, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-tab',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (isActive()) {
      <div
        [id]="'panel-' + id()"
        role="tabpanel"
        [attr.aria-labelledby]="'tab-' + id()"
        tabindex="0"
        class="focus:outline-none animate-fadeIn">
        <ng-content></ng-content>
      </div>
    }
  `
})
export class TabComponent {
  readonly id = input.required<string>();
  readonly label = input.required<string>();
  readonly icon = input<string | null>(null);
  readonly badge = input<string | number | null>(null);
  readonly disabled = input<boolean>(false);

  readonly isActive = signal<boolean>(false);
}
