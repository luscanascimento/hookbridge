import { Component, input, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LatencyQuantiles } from '../../../core/models/endpoint-health.models';

@Component({
  selector: 'app-latency-quantiles-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule],
  template: `
    <div class="p-4 rounded-xl bg-surface-900/60 border border-surface-800 space-y-3 font-sans">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <svg class="w-4 h-4 text-brand-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/>
          </svg>
          <span class="text-xs font-semibold text-surface-200 uppercase tracking-wider">Latency Quantiles & Distribution</span>
        </div>
        <span class="text-xs font-mono text-surface-400">
          Avg: <strong class="text-white">{{ latencies().averageMs }}ms</strong>
        </span>
      </div>

      <!-- Quantiles Grid -->
      <div class="grid grid-cols-2 sm:grid-cols-4 gap-2 text-center text-xs">
        <div class="p-2.5 rounded-lg bg-surface-950 border border-surface-800">
          <span class="text-[10px] text-surface-500 font-medium uppercase block">P50 (Median)</span>
          <span class="text-base font-bold font-mono text-surface-200">{{ latencies().p50Ms }}ms</span>
        </div>
        <div class="p-2.5 rounded-lg bg-surface-950 border border-surface-800">
          <span class="text-[10px] text-surface-500 font-medium uppercase block">P90</span>
          <span class="text-base font-bold font-mono text-surface-200">{{ latencies().p90Ms }}ms</span>
        </div>
        <div class="p-2.5 rounded-lg bg-surface-950 border border-surface-800">
          <span class="text-[10px] text-surface-500 font-medium uppercase block">P95</span>
          <span class="text-base font-bold font-mono" [ngClass]="latencies().p95Ms > 2000 ? 'text-rose-400' : 'text-brand-300'">
            {{ latencies().p95Ms }}ms
          </span>
        </div>
        <div class="p-2.5 rounded-lg bg-surface-950 border border-surface-800">
          <span class="text-[10px] text-surface-500 font-medium uppercase block">P99</span>
          <span class="text-base font-bold font-mono" [ngClass]="latencies().p99Ms > 3000 ? 'text-rose-400' : 'text-purple-300'">
            {{ latencies().p99Ms }}ms
          </span>
        </div>
      </div>

      <!-- Min / Max Span Bar -->
      <div class="pt-1 flex items-center justify-between text-[11px] font-mono text-surface-400">
        <span>Min: {{ latencies().minMs }}ms</span>
        <div class="flex-1 mx-3 h-1.5 bg-surface-800 rounded-full overflow-hidden flex">
          <div class="bg-gradient-to-r from-emerald-500 via-amber-500 to-rose-500 h-full rounded-full w-full"></div>
        </div>
        <span>Max: {{ latencies().maxMs }}ms</span>
      </div>
    </div>
  `
})
export class LatencyQuantilesCardComponent {
  readonly latencies = input.required<LatencyQuantiles>();
}
