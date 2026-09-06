import { Component, input, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CircuitBreakerState } from '../../../core/models/endpoint-health.models';

@Component({
  selector: 'app-circuit-breaker-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule],
  template: `
    <span
      class="px-2 py-0.5 rounded-full text-[10px] font-sans font-semibold border inline-flex items-center gap-1.5"
      [ngClass]="classes()"
      [title]="tooltip()">
      
      @if (state() === 'Closed') {
        <span class="w-1.5 h-1.5 rounded-full bg-emerald-400"></span>
        <span>Circuit: Closed</span>
      } @else if (state() === 'HalfOpen') {
        <span class="w-1.5 h-1.5 rounded-full bg-amber-400 animate-pulse"></span>
        <span>Circuit: Half-Open</span>
      } @else {
        <span class="w-1.5 h-1.5 rounded-full bg-rose-400 animate-pulse"></span>
        <span>Circuit: Open (Tripped)</span>
      }
    </span>
  `
})
export class CircuitBreakerBadgeComponent {
  readonly state = input.required<CircuitBreakerState | string>();

  readonly classes = computed(() => {
    switch (this.state()) {
      case 'Closed':
        return 'bg-emerald-500/10 text-emerald-300 border-emerald-500/30';
      case 'HalfOpen':
        return 'bg-amber-500/15 text-amber-300 border-amber-500/30';
      case 'Open':
        return 'bg-rose-500/20 text-rose-300 border-rose-500/40 shadow-sm shadow-rose-500/10';
      default:
        return 'bg-surface-800 text-surface-400 border-surface-700';
    }
  });

  readonly tooltip = computed(() => {
    switch (this.state()) {
      case 'Closed':
        return 'Circuit is closed: all webhook dispatches are actively transmitted to the destination.';
      case 'HalfOpen':
        return 'Circuit is half-open: trialing recovery dispatches after consecutive errors.';
      case 'Open':
        return 'Circuit is open: outbound deliveries are temporarily isolated due to persistent downstream failure.';
      default:
        return 'Unknown circuit state.';
    }
  });
}
