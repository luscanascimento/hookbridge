import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-12 text-center flex flex-col items-center justify-center">
      <div class="w-12 h-12 rounded-xl bg-surface-900 border border-surface-800 flex items-center justify-center text-surface-500 mb-3 shadow-inner">
        <ng-content select="[slot=icon]">
          <svg class="w-6 h-6 text-surface-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4"/>
          </svg>
        </ng-content>
      </div>

      <h3 class="text-sm font-semibold text-white">{{ title() }}</h3>
      <p class="mt-1 text-xs text-surface-400 max-w-sm">{{ description() }}</p>

      <div class="mt-5 flex items-center gap-3">
        <ng-content select="[slot=actions]"></ng-content>
      </div>
    </div>
  `
})
export class EmptyStateComponent {
  readonly title = input.required<string>();
  readonly description = input<string>('No records matching your criteria were found.');
}
