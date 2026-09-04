import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from './toast.service';
import { ToastComponent } from './toast.component';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [CommonModule, ToastComponent],
  template: `
    <div
      aria-live="polite"
      aria-atomic="true"
      class="fixed bottom-4 right-4 z-50 flex flex-col-reverse gap-2.5 max-w-sm w-full pointer-events-none px-4 sm:px-0">
      @for (t of toastService.toasts(); track t.id) {
        <app-toast
          [toast]="t"
          (dismissed)="toastService.dismiss($event)"
          class="animate-slideUp pointer-events-auto">
        </app-toast>
      }
    </div>
  `
})
export class ToastContainerComponent {
  readonly toastService = inject(ToastService);
}
