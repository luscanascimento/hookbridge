import { Component, HostListener, computed, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SlideOverWidth = 'md' | 'lg' | 'xl' | '2xl';

@Component({
  selector: 'app-slide-over',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (isOpen()) {
      <div class="fixed inset-0 z-50 overflow-hidden">
        <!-- Backdrop -->
        <div
          class="fixed inset-0 bg-surface-950/75 backdrop-blur-xs transition-opacity duration-300"
          (click)="onBackdropClick()">
        </div>

        <div class="fixed inset-y-0 right-0 max-w-full flex pl-10">
          <!-- Slide Panel -->
          <div
            class="w-screen bg-surface-900 border-l border-surface-800 shadow-2xl shadow-black flex flex-col justify-between"
            [ngClass]="widthClasses()"
            role="dialog"
            aria-modal="true">

            <!-- Top Header -->
            <div class="px-6 py-4 border-b border-surface-800 flex items-center justify-between bg-surface-950/40">
              <div>
                <h3 class="text-base font-semibold text-white tracking-tight">
                  <ng-content select="[slot=title]">{{ title() }}</ng-content>
                </h3>
                @if (subtitle()) {
                  <p class="text-xs text-surface-400 mt-0.5">{{ subtitle() }}</p>
                }
              </div>
              <button
                (click)="close()"
                type="button"
                class="p-1.5 rounded-lg text-surface-400 hover:text-white hover:bg-surface-800 transition-colors focus:outline-none"
                aria-label="Close drawer">
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
                </svg>
              </button>
            </div>

            <!-- Body Content -->
            <div class="flex-1 px-6 py-5 overflow-y-auto font-sans text-sm text-surface-200">
              <ng-content></ng-content>
            </div>

            <!-- Footer -->
            @if (showFooter()) {
              <div class="px-6 py-4 border-t border-surface-800 bg-surface-950/60 flex items-center justify-end gap-3">
                <ng-content select="[slot=footer]"></ng-content>
              </div>
            }
          </div>
        </div>
      </div>
    }
  `
})
export class SlideOverComponent {
  readonly isOpen = input<boolean>(false);
  readonly title = input<string>('');
  readonly subtitle = input<string | null>(null);
  readonly width = input<SlideOverWidth>('lg');
  readonly closeOnBackdrop = input<boolean>(true);
  readonly closeOnEscape = input<boolean>(true);
  readonly showFooter = input<boolean>(false);

  readonly closed = output<void>();

  readonly widthClasses = computed(() => {
    switch (this.width()) {
      case 'md': return 'max-w-md';
      case 'xl': return 'max-w-2xl';
      case '2xl': return 'max-w-4xl';
      default: return 'max-w-xl';
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
