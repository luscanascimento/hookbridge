import { Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SignalRService } from '../../../core/signalr/services/signalr.service';

@Component({
  selector: 'app-live-indicator',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="inline-flex items-center gap-2 px-2.5 py-1 rounded-full text-xs font-medium border bg-surface-900" [ngClass]="containerClass()">
      <span class="relative flex h-2 w-2">
        @if (status() === 'connected') {
          <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
        }
        <span class="relative inline-flex rounded-full h-2 w-2" [ngClass]="dotClass()"></span>
      </span>
      <span class="tracking-wide uppercase text-[10px] font-semibold">{{ label() }}</span>
    </div>
  `
})
export class LiveIndicatorComponent {
  private readonly signalR = inject(SignalRService);

  readonly status = this.signalR.status;

  readonly label = computed(() => {
    switch (this.status()) {
      case 'connected': return 'Live Stream';
      case 'connecting': return 'Connecting...';
      case 'reconnecting': return 'Reconnecting...';
      default: return 'Offline';
    }
  });

  readonly containerClass = computed(() => {
    switch (this.status()) {
      case 'connected': return 'border-emerald-500/30 text-emerald-300';
      case 'connecting':
      case 'reconnecting': return 'border-amber-500/30 text-amber-300';
      default: return 'border-surface-700 text-surface-400';
    }
  });

  readonly dotClass = computed(() => {
    switch (this.status()) {
      case 'connected': return 'bg-emerald-500';
      case 'connecting':
      case 'reconnecting': return 'bg-amber-500 animate-pulse';
      default: return 'bg-surface-500';
    }
  });
}
