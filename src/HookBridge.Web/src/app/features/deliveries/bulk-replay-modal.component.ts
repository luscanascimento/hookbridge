import { Component, inject, input, output, signal, effect, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ModalComponent } from '../../shared/components/ui/modal.component';
import { ButtonComponent } from '../../shared/components/ui/button.component';
import { DeliveryService } from '../../core/services/delivery.service';
import { ToastService } from '../../shared/components/ui/toast/toast.service';
import { Endpoint, BulkReplayDeliveriesResponse } from '../../shared/models/control-plane.models';
import { DeliveryStatus } from '../../core/signalr/models/signalr.models';

@Component({
  selector: 'app-bulk-replay-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, ModalComponent, ButtonComponent],
  template: `
    <app-modal
      [isOpen]="isOpen()"
      title="Bulk Replay Deliveries"
      size="md"
      (closed)="onClose()">
      
      <div class="space-y-4 text-xs font-sans">
        <p class="text-surface-300">
          Replay batches of deliveries matching criteria below. Each replayed delivery generates fresh HMAC signature headers while preserving lineage.
        </p>

        <!-- Warning Alert -->
        <div class="p-3 bg-amber-950/30 border border-amber-800/40 rounded-xl text-amber-300 flex items-start gap-2.5">
          <svg class="w-4 h-4 text-amber-400 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>
          </svg>
          <div class="text-[11px] leading-relaxed">
            Ensure destination webhook endpoints can handle outbound traffic spikes and process idempotency keys properly.
          </div>
        </div>

        <!-- Filter Fields -->
        <div class="space-y-3">
          <!-- Status Filter -->
          <div>
            <label class="block text-[11px] font-semibold text-surface-400 uppercase tracking-wider mb-1">
              Target Status
            </label>
            <select
              [(ngModel)]="targetStatus"
              class="w-full bg-surface-950 border border-surface-800 rounded-lg p-2.5 text-white font-mono text-xs focus:border-brand-500 focus:outline-none">
              <option value="Failed">Failed Only (HTTP 4xx / 5xx / Timeout)</option>
              <option value="DeadLettered">Dead-Lettered Only (Exhausted Retries)</option>
              <option value="">All Statuses</option>
            </select>
          </div>

          <!-- Target Endpoint -->
          <div>
            <label class="block text-[11px] font-semibold text-surface-400 uppercase tracking-wider mb-1">
              Filter by Endpoint (Optional)
            </label>
            <select
              [(ngModel)]="targetEndpointId"
              class="w-full bg-surface-950 border border-surface-800 rounded-lg p-2.5 text-white font-mono text-xs focus:border-brand-500 focus:outline-none">
              <option [ngValue]="null">All Endpoints</option>
              @for (ep of endpoints(); track ep.id) {
                <option [ngValue]="ep.id">
                  {{ ep.targetUrl }}
                </option>
              }
            </select>
          </div>

          <!-- Event Type -->
          <div>
            <label class="block text-[11px] font-semibold text-surface-400 uppercase tracking-wider mb-1">
              Filter by Event Type (Optional)
            </label>
            <input
              type="text"
              [(ngModel)]="targetEventType"
              placeholder="e.g. order.created or invoice.*"
              class="w-full bg-surface-950 border border-surface-800 rounded-lg p-2.5 text-white font-mono text-xs focus:border-brand-500 focus:outline-none" />
          </div>

          <!-- Max Batch Count -->
          <div>
            <label class="block text-[11px] font-semibold text-surface-400 uppercase tracking-wider mb-1">
              Max Batch Count
            </label>
            <select
              [(ngModel)]="maxCount"
              class="w-full bg-surface-950 border border-surface-800 rounded-lg p-2.5 text-white font-mono text-xs focus:border-brand-500 focus:outline-none">
              <option [ngValue]="10">10 deliveries</option>
              <option [ngValue]="25">25 deliveries</option>
              <option [ngValue]="50">50 deliveries (Recommended)</option>
              <option [ngValue]="100">100 deliveries (Max limit)</option>
            </select>
          </div>
        </div>
      </div>

      <div slot="footer" class="flex items-center justify-end gap-3">
        <button
          (click)="onClose()"
          type="button"
          class="px-3 py-1.5 bg-surface-800 hover:bg-surface-700 text-surface-300 text-xs rounded-lg transition-colors">
          Cancel
        </button>
        <app-button
          variant="primary"
          size="sm"
          [loading]="isLoading()"
          (clicked)="executeBulkReplay()">
          <svg class="w-3.5 h-3.5 mr-1.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
          </svg>
          Replay Deliveries
        </app-button>
      </div>
    </app-modal>
  `
})
export class BulkReplayModalComponent {
  private readonly deliveryService = inject(DeliveryService);
  private readonly toast = inject(ToastService);

  readonly isOpen = input<boolean>(false);
  readonly endpoints = input<Endpoint[]>([]);
  readonly initialStatus = input<DeliveryStatus | string | null>('Failed');
  readonly initialEndpointId = input<string | null>(null);
  readonly initialEventType = input<string | null>(null);

  readonly closed = output<void>();
  readonly replayed = output<BulkReplayDeliveriesResponse>();

  readonly isLoading = signal<boolean>(false);
  readonly targetStatus = signal<string>('Failed');
  readonly targetEndpointId = signal<string | null>(null);
  readonly targetEventType = signal<string>('');
  readonly maxCount = signal<number>(50);

  constructor() {
    effect(() => {
      if (this.isOpen()) {
        this.targetStatus.set((this.initialStatus() as string) || 'Failed');
        this.targetEndpointId.set(this.initialEndpointId());
        this.targetEventType.set(this.initialEventType() || '');
        this.maxCount.set(50);
      }
    });
  }

  executeBulkReplay(): void {
    this.isLoading.set(true);

    const command = {
      status: this.targetStatus() || undefined,
      endpointId: this.targetEndpointId() || undefined,
      eventType: this.targetEventType().trim() || undefined,
      maxCount: this.maxCount()
    };

    this.deliveryService.bulkReplay(command).subscribe({
      next: (res) => {
        this.isLoading.set(false);
        this.toast.success(
          `Successfully queued ${res.replayedCount} delivery replays for dispatch.`,
          'Bulk Replay Executed'
        );
        this.replayed.emit(res);
        this.onClose();
      },
      error: (err) => {
        this.isLoading.set(false);
        const msg = err.error?.detail || 'Failed to execute bulk replay.';
        this.toast.error(msg, 'Bulk Replay Error');
      }
    });
  }

  onClose(): void {
    this.closed.emit();
  }
}
