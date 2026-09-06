import { Component, input, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-health-score-gauge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule],
  template: `
    <div class="inline-flex items-center gap-2" [title]="'Reliability Score: ' + score() + '% (' + label() + ')'">
      <!-- Circular Progress Ring / Badge -->
      @if (size() === 'lg') {
        <div class="relative w-16 h-16 flex items-center justify-center shrink-0">
          <svg class="w-full h-full -rotate-90" viewBox="0 0 36 36">
            <!-- Background circle -->
            <path
              class="text-surface-800"
              stroke-width="3.5"
              stroke="currentColor"
              fill="none"
              d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" />
            <!-- Progress circle -->
            <path
              [ngClass]="strokeColorClass()"
              [attr.stroke-dasharray]="score() + ', 100'"
              stroke-width="3.5"
              stroke-linecap="round"
              stroke="currentColor"
              fill="none"
              d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" />
          </svg>
          <div class="absolute inset-0 flex flex-col items-center justify-center">
            <span class="text-sm font-bold font-mono text-white leading-none">{{ score() }}</span>
            <span class="text-[9px] text-surface-400 font-sans uppercase leading-none mt-0.5">%</span>
          </div>
        </div>
      } @else if (size() === 'md') {
        <div
          class="px-2.5 py-1 rounded-lg border font-mono text-xs font-bold inline-flex items-center gap-1.5 shadow-sm"
          [ngClass]="badgeColorClasses()">
          <span class="w-2 h-2 rounded-full" [ngClass]="dotColorClass()"></span>
          <span>{{ score() }}%</span>
          @if (showLabel()) {
            <span class="text-[10px] font-sans font-medium uppercase opacity-80">{{ label() }}</span>
          }
        </div>
      } @else {
        <!-- 'sm' badge -->
        <span
          class="px-2 py-0.5 rounded-full text-[11px] font-mono font-semibold border inline-flex items-center gap-1"
          [ngClass]="badgeColorClasses()">
          <span class="w-1.5 h-1.5 rounded-full" [ngClass]="dotColorClass()"></span>
          <span>{{ score() }}%</span>
        </span>
      }
    </div>
  `
})
export class HealthScoreGaugeComponent {
  readonly score = input.required<number>();
  readonly size = input<'sm' | 'md' | 'lg'>('sm');
  readonly showLabel = input<boolean>(false);

  readonly label = computed(() => {
    const s = this.score();
    if (s >= 90) return 'Healthy';
    if (s >= 60) return 'Degraded';
    return 'Critical';
  });

  readonly strokeColorClass = computed(() => {
    const s = this.score();
    if (s >= 90) return 'text-emerald-400';
    if (s >= 60) return 'text-amber-400';
    return 'text-rose-400';
  });

  readonly dotColorClass = computed(() => {
    const s = this.score();
    if (s >= 90) return 'bg-emerald-400';
    if (s >= 60) return 'bg-amber-400';
    return 'bg-rose-400 animate-pulse';
  });

  readonly badgeColorClasses = computed(() => {
    const s = this.score();
    if (s >= 90) {
      return 'bg-emerald-500/15 text-emerald-300 border-emerald-500/30';
    }
    if (s >= 60) {
      return 'bg-amber-500/15 text-amber-300 border-amber-500/30';
    }
    return 'bg-rose-500/15 text-rose-300 border-rose-500/30';
  });
}
