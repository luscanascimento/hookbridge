import { Injectable, signal } from '@angular/core';
import { ToastItem, ToastType, ToastAction } from './toast.models';

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  private readonly _toasts = signal<ToastItem[]>([]);
  readonly toasts = this._toasts.asReadonly();

  private timeouts = new Map<string, any>();

  show(options: {
    type?: ToastType;
    title?: string;
    message: string;
    durationMs?: number;
    action?: ToastAction;
  }): string {
    const id = 'toast-' + Math.random().toString(36).substring(2, 9);
    const duration = options.durationMs ?? 5000;

    const item: ToastItem = {
      id,
      type: options.type ?? 'info',
      title: options.title,
      message: options.message,
      durationMs: duration,
      action: options.action,
      createdAt: Date.now()
    };

    this._toasts.update(current => [item, ...current.slice(0, 4)]); // max 5 toasts

    if (duration > 0) {
      const timer = setTimeout(() => {
        this.dismiss(id);
      }, duration);
      this.timeouts.set(id, timer);
    }

    return id;
  }

  success(message: string, title?: string, durationMs?: number): string {
    return this.show({ type: 'success', title: title ?? 'Success', message, durationMs });
  }

  error(message: string, title?: string, durationMs?: number): string {
    return this.show({ type: 'error', title: title ?? 'Error', message, durationMs: durationMs ?? 7000 });
  }

  info(message: string, title?: string, durationMs?: number): string {
    return this.show({ type: 'info', title: title ?? 'Info', message, durationMs });
  }

  warning(message: string, title?: string, durationMs?: number): string {
    return this.show({ type: 'warning', title: title ?? 'Warning', message, durationMs: durationMs ?? 6000 });
  }

  dismiss(id: string): void {
    if (this.timeouts.has(id)) {
      clearTimeout(this.timeouts.get(id));
      this.timeouts.delete(id);
    }
    this._toasts.update(current => current.filter(t => t.id !== id));
  }

  clearAll(): void {
    for (const timer of this.timeouts.values()) {
      clearTimeout(timer);
    }
    this.timeouts.clear();
    this._toasts.set([]);
  }
}
