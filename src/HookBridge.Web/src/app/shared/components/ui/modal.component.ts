import { Component, HostListener, computed, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

export type ModalSize = 'sm' | 'md' | 'lg' | 'xl' | '2xl';

@Component({
  selector: 'app-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (isOpen()) {
      <div class="fixed inset-0 z-50 flex items-center justify-center p-4 overflow-y-auto">
        <!-- Backdrop -->
        <div
          class="fixed inset-0 bg-surface-950/80 backdrop-blur-sm transition-opacity"
          (click)="onBackdropClick()">
        </div>

        <!-- Modal Card -->
        <div
          class="relative w-full bg-surface-900 border border-surface-700/80 rounded-2xl shadow-2xl shadow-black/60 overflow-hidden transform transition-all z-10 my-8"
          [ngClass]="sizeClasses()"
          role="dialog"
          aria-modal="true">

          <!-- Header -->
          <div class="px-6 py-4 border-b border-surface-800 flex items-center justify-between">
            <h3 class="text-base font-semibold text-white tracking-tight">
              <ng-content select="[slot=title]">{{ title() }}</ng-content>
            </h3>
            <button
              (click)="close()"
              type="button"
              class="p-1 rounded-lg text-surface-400 hover:text-white hover:bg-surface-800 transition-colors focus:outline-none"
              aria-label="Close modal">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          </div>

          <!-- Body -->
          <div class="px-6 py-5 max-h-[70vh] overflow-y-auto font-sans text-sm text-surface-200">
            <ng-content></ng-content>
          </div>

          <!-- Footer -->
          @if (showFooter()) {
            <div class="px-6 py-4 border-t border-surface-800 bg-surface-950/40 flex items-center justify-end gap-3">
              <ng-content select="[slot=footer]"></ng-content>
            </div>
          }
        </div>
      </div>
    }
  `
})
export class ModalComponent {
  readonly isOpen = input<boolean>(false);
  readonly title = input<string>('');
  readonly size = input<ModalSize>('md');
  readonly closeOnBackdrop = input<boolean>(true);
  readonly closeOnEscape = input<boolean>(true);
  readonly showFooter = input<boolean>(true);

  readonly closed = output<void>();

  readonly sizeClasses = computed(() => {
    switch (this.size()) {
      case 'sm': return 'max-w-sm';
      case 'lg': return 'max-w-2xl';
      case 'xl': return 'max-w-4xl';
      case '2xl': return 'max-w-5xl';
      default: return 'max-w-lg';
    }
  });

  @HostListener('document:keydown.escape', ['$event'])
  onEscape(event: Event): void {
    if (this.isOpen() && this.closeOnEscape()) {
      event.preventDefault();
      this.close();
    }
  }

  onBackdropClick(): void {
    if (this.closeOnBackdrop()) {
      this.close();
    }
  }

  close(): void {
    this.closed.emit();
  }
}
