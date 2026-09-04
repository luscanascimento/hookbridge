import { Component, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SkeletonLoaderComponent } from './skeleton-loader.component';

export type MetricTrend = 'up' | 'down' | 'neutral';

@Component({
  selector: 'app-metric-card',
  standalone: true,
  imports: [CommonModule, SkeletonLoaderComponent],
  template: `
    <div class="bg-surface-900/80 border border-surface-800 rounded-xl p-4 shadow-sm relative overflow-hidden flex flex-col justify-between">
      @if (loading()) {
        <div class="space-y-3">
          <div class="flex justify-between items-center">
            <app-skeleton-loader customClass="h-3 w-1/3"></app-skeleton-loader>
            <app-skeleton-loader customClass="h-4 w-4 rounded-full"></app-skeleton-loader>
          </div>
          <app-skeleton-loader customClass="h-8 w-1/2"></app-skeleton-loader>
          <div class="flex justify-between items-end pt-2">
            <app-skeleton-loader customClass="h-3 w-1/4"></app-skeleton-loader>
            <app-skeleton-loader customClass="h-6 w-20"></app-skeleton-loader>
          </div>
        </div>
      } @else {
        <!-- Top Row: Label and Icon -->
        <div class="flex items-center justify-between">
          <span class="text-xs font-medium text-surface-400 uppercase tracking-wider">{{ label() }}</span>
          @if (icon()) {
            <span class="text-surface-500 text-sm">{{ icon() }}</span>
          }
        </div>

        <!-- Main Metric Value -->
        <div class="mt-2 flex items-baseline gap-1.5">
          <span class="text-2xl font-bold tracking-tight text-white font-mono">{{ value() }}</span>
          @if (unit()) {
            <span class="text-xs font-medium text-surface-400">{{ unit() }}</span>
          }
        </div>

        <!-- Bottom Row: Trend and Sparkline -->
        <div class="mt-3 pt-2 border-t border-surface-800/60 flex items-end justify-between gap-2">
          <!-- Trend Delta -->
          @if (delta()) {
            <div class="flex items-center gap-1 text-[11px] font-medium">
              <span [ngClass]="trendColorClass()" class="inline-flex items-center gap-0.5">
                @if (trend() === 'up') {
                  <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6"/>
                  </svg>
                } @else if (trend() === 'down') {
                  <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 17h8m0 0v-8m0 8l-8-8-4 4-6-6"/>
                  </svg>
                }
                {{ delta() }}
              </span>
              @if (period()) {
                <span class="text-surface-500 text-[10px]">{{ period() }}</span>
              }
            </div>
          } @else {
            <div></div>
          }

          <!-- SVG Sparkline -->
          @if (sparklinePoints().length > 0) {
            <div class="w-24 h-7 flex-shrink-0">
              <svg viewBox="0 0 100 30" class="w-full h-full overflow-visible">
                <defs>
                  <linearGradient [id]="gradientId()" x1="0%" y1="0%" x2="0%" y2="100%">
                    <stop offset="0%" [attr.stop-color]="sparklineColor()" stop-opacity="0.3" />
                    <stop offset="100%" [attr.stop-color]="sparklineColor()" stop-opacity="0.0" />
                  </linearGradient>
                </defs>
                <!-- Filled Area -->
                <polygon
                  [attr.points]="sparklineAreaPoints()"
                  [attr.fill]="'url(#' + gradientId() + ')'" />
                <!-- Line -->
                <polyline
                  fill="none"
                  [attr.stroke]="sparklineColor()"
                  stroke-width="1.75"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  [attr.points]="sparklinePoints()" />
              </svg>
            </div>
          }
        </div>
      }
    </div>
  `
})
export class MetricCardComponent {
  readonly label = input.required<string>();
  readonly value = input.required<string | number>();
  readonly unit = input<string | null>(null);
  readonly icon = input<string | null>(null);
  readonly delta = input<string | null>(null);
  readonly trend = input<MetricTrend>('neutral');
  readonly period = input<string>('vs last period');
  readonly sparklineData = input<number[]>([]);
  readonly loading = input<boolean>(false);

  readonly gradientId = computed(() => 'sparkline-grad-' + Math.random().toString(36).substring(2, 9));

  readonly trendColorClass = computed(() => {
    switch (this.trend()) {
      case 'up': return 'text-emerald-400';
      case 'down': return 'text-rose-400';
      default: return 'text-surface-400';
    }
  });

  readonly sparklineColor = computed(() => {
    switch (this.trend()) {
      case 'up': return '#34d399'; // emerald-400
      case 'down': return '#fb7185'; // rose-400
      default: return '#38bdf8'; // sky-400 / brand
    }
  });

  readonly sparklinePoints = computed(() => {
    const data = this.sparklineData();
    if (!data || data.length < 2) return '';

    const min = Math.min(...data);
    const max = Math.max(...data);
    const range = max - min || 1;
    const width = 100;
    const height = 26; // leave 4px padding
    const padding = 2;

    const step = width / (data.length - 1);

    return data
      .map((val, idx) => {
        const x = Math.round(idx * step * 10) / 10;
        const normalized = (val - min) / range;
        const y = Math.round((height - normalized * (height - padding * 2) + padding) * 10) / 10;
        return `${x},${y}`;
      })
      .join(' ');
  });

  readonly sparklineAreaPoints = computed(() => {
    const points = this.sparklinePoints();
    if (!points) return '';
    return `0,30 ${points} 100,30`;
  });
}
