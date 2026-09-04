import { Component, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium border" [ngClass]="badgeClass()">
      <span class="w-1.5 h-1.5 rounded-full mr-1.5" [ngClass]="dotClass()"></span>
      {{ label() }}
    </span>
  `
})
export class StatusBadgeComponent {
  readonly status = input.required<string>();

  readonly label = computed(() => this.status());

  readonly badgeClass = computed(() => {
    switch (this.status().toLowerCase()) {
      case 'success':
      case 'active':
        return 'bg-emerald-950/50 text-emerald-300 border-emerald-800/60';
      case 'failed':
      case 'deadlettered':
        return 'bg-rose-950/50 text-rose-300 border-rose-800/60';
      case 'pending':
      case 'dispatched':
        return 'bg-amber-950/50 text-amber-300 border-amber-800/60';
      case 'disabled':
        return 'bg-surface-900 text-surface-400 border-surface-700';
      default:
        return 'bg-surface-800 text-surface-300 border-surface-700';
    }
  });

  readonly dotClass = computed(() => {
    switch (this.status().toLowerCase()) {
      case 'success':
      case 'active':
        return 'bg-emerald-400';
      case 'failed':
      case 'deadlettered':
        return 'bg-rose-400';
      case 'pending':
      case 'dispatched':
        return 'bg-amber-400';
      case 'disabled':
        return 'bg-surface-500';
      default:
        return 'bg-surface-400';
    }
  });
}
