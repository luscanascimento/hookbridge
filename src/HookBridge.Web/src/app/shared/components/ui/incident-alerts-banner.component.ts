import { Component, input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { EndpointIncidentAlert } from '../../../core/models/endpoint-health.models';

@Component({
  selector: 'app-incident-alerts-banner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, DatePipe],
  template: `
    <div class="space-y-2">
      @for (incident of incidents(); track incident.id) {
        <div
          class="p-3 rounded-xl border flex items-start justify-between gap-3 text-xs"
          [ngClass]="getCardClasses(incident.severity)">
          
          <div class="flex items-start gap-2.5 min-w-0">
            <!-- Icon -->
            <div class="p-1 rounded mt-0.5 shrink-0" [ngClass]="getIconClasses(incident.severity)">
              @if (incident.severity === 'Critical') {
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>
                </svg>
              } @else {
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
                </svg>
              }
            </div>

            <!-- Content -->
            <div class="space-y-0.5 min-w-0">
              <div class="flex items-center gap-2">
                <span class="font-bold text-white">{{ incident.title }}</span>
                <span class="px-1.5 py-0.2 rounded text-[9px] font-sans font-bold uppercase" [ngClass]="getBadgeClasses(incident.severity)">
                  {{ incident.severity }}
                </span>
              </div>
              <p class="text-surface-300 font-sans text-[11px] leading-relaxed break-words">
                {{ incident.description }}
              </p>
            </div>
          </div>

          <!-- Timestamp -->
          <div class="text-[10px] font-mono text-surface-400 shrink-0 text-right">
            {{ incident.triggeredAt | date:'HH:mm:ss' }}
          </div>
        </div>
      }
    </div>
  `
})
export class IncidentAlertsBannerComponent {
  readonly incidents = input.required<EndpointIncidentAlert[]>();

  getCardClasses(severity: string): string {
    switch (severity) {
      case 'Critical':
        return 'bg-rose-950/30 border-rose-800/60 shadow-lg shadow-rose-950/20';
      case 'Warning':
        return 'bg-amber-950/30 border-amber-800/60';
      default:
        return 'bg-sky-950/30 border-sky-800/60';
    }
  }

  getIconClasses(severity: string): string {
    switch (severity) {
      case 'Critical': return 'bg-rose-500/20 text-rose-400';
      case 'Warning': return 'bg-amber-500/20 text-amber-400';
      default: return 'bg-sky-500/20 text-sky-400';
    }
  }

  getBadgeClasses(severity: string): string {
    switch (severity) {
      case 'Critical': return 'bg-rose-500/20 text-rose-300 border border-rose-500/30';
      case 'Warning': return 'bg-amber-500/20 text-amber-300 border border-amber-500/30';
      default: return 'bg-sky-500/20 text-sky-300 border border-sky-500/30';
    }
  }
}
