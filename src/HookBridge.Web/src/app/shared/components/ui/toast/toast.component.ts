import { Component, computed, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastItem } from './toast.models';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div
      role="alert"
      class="flex items-start gap-3 p-3.5 rounded-xl border shadow-xl backdrop-blur-md transition-all duration-300 pointer-events-auto"
      [ngClass]="containerClasses()">

      <!-- Icon -->
      <div class="flex-shrink-0 mt-0.5" [ngClass]="iconColorClass()">
        @switch (toast().type) {
          @case ('success') {
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/>
            </svg>
          }
          @case ('error') {
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
            </svg>
          }
          @case ('warning') {
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>
            </svg>
          }
          @case ('info') {
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
            </svg>
          }
        }
      </div>

      <!-- Text Content -->
      <div class="flex-1 min-w-0 pr-1">
        @if (toast().title) {
          <h4 class="text-xs font-semibold text-white tracking-tight">{{ toast().title }}</h4>
        }
        <p class="text-xs text-surface-300 mt-0.5 break-words">{{ toast().message }}</p>

        @if (toast().action; as action) {
          <button
            type="button"
            (click)="action.onClick()"
            class="mt-2 text-[11px] font-semibold text-brand-400 hover:text-brand-300 underline underline-offset-2">
            {{ action.label }}
          </button>
        }
      </div>

      <!-- Close Button -->
      <button
        type="button"
        (click)="dismissed.emit(toast().id)"
        class="flex-shrink-0 text-surface-400 hover:text-white p-1 rounded-md hover:bg-surface-800 transition-colors"
        aria-label="Dismiss notification">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
        </svg>
      </button>
    </div>
  `
})
export class ToastComponent {
  readonly toast = input.required<ToastItem>();
  readonly dismissed = output<string>();

  readonly containerClasses = computed(() => {
    switch (this.toast().type) {
      case 'success':
        return 'bg-surface-900/95 border-emerald-800/80 text-surface-100 shadow-emerald-950/30';
      case 'error':
        return 'bg-surface-900/95 border-rose-800/80 text-surface-100 shadow-rose-950/30';
      case 'warning':
        return 'bg-surface-900/95 border-amber-800/80 text-surface-100 shadow-amber-950/30';
      case 'info':
      default:
        return 'bg-surface-900/95 border-surface-700 text-surface-100 shadow-black/40';
    }
  });

  readonly iconColorClass = computed(() => {
    switch (this.toast().type) {
      case 'success': return 'text-emerald-400';
      case 'error': return 'text-rose-400';
      case 'warning': return 'text-amber-400';
      case 'info':
      default: return 'text-sky-400';
    }
  });
}
