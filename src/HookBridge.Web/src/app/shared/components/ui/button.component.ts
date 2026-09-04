import { Component, computed, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

export type ButtonVariant = 'primary' | 'secondary' | 'outline' | 'danger' | 'ghost';
export type ButtonSize = 'sm' | 'md' | 'lg';

@Component({
  selector: 'app-button',
  standalone: true,
  imports: [CommonModule],
  template: `
    <button
      [type]="type()"
      [disabled]="disabled() || loading()"
      (click)="onClick($event)"
      class="inline-flex items-center justify-center font-medium rounded-lg transition-all focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:opacity-50 disabled:cursor-not-allowed select-none"
      [ngClass]="[variantClasses(), sizeClasses(), customClass()]">
      @if (loading()) {
        <svg class="animate-spin -ml-0.5 mr-2 h-3.5 w-3.5" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
          <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
          <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
        </svg>
      }
      <ng-content select="[slot=icon-left]"></ng-content>
      <span class="truncate"><ng-content></ng-content></span>
      <ng-content select="[slot=icon-right]"></ng-content>
    </button>
  `
})
export class ButtonComponent {
  readonly variant = input<ButtonVariant>('primary');
  readonly size = input<ButtonSize>('md');
  readonly type = input<'button' | 'submit' | 'reset'>('button');
  readonly disabled = input<boolean>(false);
  readonly loading = input<boolean>(false);
  readonly customClass = input<string>('');

  readonly clicked = output<MouseEvent>();

  readonly variantClasses = computed(() => {
    switch (this.variant()) {
      case 'primary':
        return 'bg-brand-600 hover:bg-brand-500 text-white shadow-sm shadow-brand-600/20 active:bg-brand-700';
      case 'secondary':
        return 'bg-surface-800 hover:bg-surface-700 text-surface-200 border border-surface-700 active:bg-surface-900';
      case 'outline':
        return 'bg-transparent hover:bg-surface-800 text-surface-300 hover:text-white border border-surface-700 active:bg-surface-900';
      case 'danger':
        return 'bg-rose-600/90 hover:bg-rose-500 text-white shadow-sm shadow-rose-600/20 active:bg-rose-700';
      case 'ghost':
        return 'bg-transparent hover:bg-surface-800/60 text-surface-400 hover:text-surface-200 active:bg-surface-800';
      default:
        return 'bg-brand-600 text-white';
    }
  });

  readonly sizeClasses = computed(() => {
    switch (this.size()) {
      case 'sm':
        return 'px-2.5 py-1 text-xs gap-1.5';
      case 'lg':
        return 'px-4 py-2.5 text-base gap-2.5';
      default:
        return 'px-3.5 py-1.5 text-sm gap-2';
    }
  });

  onClick(event: MouseEvent): void {
    if (!this.disabled() && !this.loading()) {
      this.clicked.emit(event);
    }
  }
}
