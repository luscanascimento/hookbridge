import { Component, input, output, signal, effect, inject, ChangeDetectionStrategy, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { SlideOverComponent } from '../../shared/components/ui/slide-over.component';
import { SkeletonLoaderComponent } from '../../shared/components/ui/skeleton-loader.component';
import { HealthScoreGaugeComponent } from '../../shared/components/ui/health-score-gauge.component';
import { CircuitBreakerBadgeComponent } from '../../shared/components/ui/circuit-breaker-badge.component';
import { LatencyQuantilesCardComponent } from '../../shared/components/ui/latency-quantiles-card.component';
import { IncidentAlertsBannerComponent } from '../../shared/components/ui/incident-alerts-banner.component';
import { EndpointService } from '../../core/services/endpoint.service';
import { EndpointHealth } from '../../core/models/endpoint-health.models';

@Component({
  selector: 'app-endpoint-health-drawer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DatePipe,
    RouterLink,
    SlideOverComponent,
    SkeletonLoaderComponent,
    HealthScoreGaugeComponent,
    CircuitBreakerBadgeComponent,
    LatencyQuantilesCardComponent,
    IncidentAlertsBannerComponent
  ],
  template: `
    <app-slide-over
      [isOpen]="isOpen()"
      [width]="'2xl'"
      (closed)="onClose()">
      
      <!-- Custom Header -->
      <div slot="title" class="flex items-center gap-3">
        @if (healthDetail(); as health) {
          <app-health-score-gauge
            [score]="health.healthScorePercent"
            size="md">
          </app-health-score-gauge>
        }
        <div>
          <div class="flex items-center gap-2">
            <span class="text-base font-bold text-white font-mono truncate max-w-sm" [title]="targetUrl()">
              {{ targetUrl() || 'Endpoint Health & SLA' }}
            </span>
          </div>
          <div class="flex items-center gap-2 mt-0.5 text-xs text-surface-400 font-mono">
            <span>ID: {{ endpointId() }}</span>
            @if (healthDetail(); as health) {
              <span>&bull;</span>
              <app-circuit-breaker-badge [state]="health.circuitState"></app-circuit-breaker-badge>
            }
          </div>
        </div>
      </div>

      <!-- Drawer Body Content -->
      @if (isLoading()) {
        <div class="space-y-4 py-6">
          <app-skeleton-loader customClass="h-24 w-full"></app-skeleton-loader>
          <app-skeleton-loader customClass="h-32 w-full"></app-skeleton-loader>
          <app-skeleton-loader customClass="h-48 w-full"></app-skeleton-loader>
        </div>
      } @else if (errorState()) {
        <div class="p-8 text-center">
          <div class="w-12 h-12 rounded-full bg-rose-500/10 text-rose-400 flex items-center justify-center mx-auto mb-3">
            <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>
            </svg>
          </div>
          <div class="text-sm font-semibold text-white">Failed to load health metrics</div>
          <p class="text-xs text-surface-400 mt-1 max-w-sm mx-auto">{{ errorState() }}</p>
          <button (click)="retryLoad()" class="mt-4 px-3 py-1.5 bg-surface-800 hover:bg-surface-700 text-surface-200 text-xs rounded-lg transition-colors">
            Try Again
          </button>
        </div>
      } @else if (healthDetail(); as health) {
        <div class="space-y-6 pb-6">
          
          <!-- Key Metrics Grid -->
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 p-3.5 bg-surface-950/80 rounded-xl border border-surface-800 text-xs font-sans">
            <div>
              <div class="text-[10px] text-surface-500 font-semibold uppercase tracking-wider">Uptime SLA (24h)</div>
              <div class="text-base font-bold font-mono text-emerald-400 mt-0.5">
                {{ health.uptimePercent }}%
              </div>
            </div>

            <div>
              <div class="text-[10px] text-surface-500 font-semibold uppercase tracking-wider">Deliveries (24h)</div>
              <div class="text-base font-bold font-mono text-surface-200 mt-0.5">
                {{ health.totalDeliveries }} <span class="text-xs text-surface-500 font-normal">({{ health.successCount }} ok)</span>
              </div>
            </div>

            <div>
              <div class="text-[10px] text-surface-500 font-semibold uppercase tracking-wider">Error Rate</div>
              <div class="text-base font-bold font-mono mt-0.5" [ngClass]="health.errorRatePercent > 10 ? 'text-rose-400' : 'text-surface-300'">
                {{ health.errorRatePercent }}%
              </div>
            </div>

            <div>
              <div class="text-[10px] text-surface-500 font-semibold uppercase tracking-wider">Consecutive Fails</div>
              <div class="text-base font-bold font-mono mt-0.5" [ngClass]="health.consecutiveFailures > 0 ? 'text-rose-400 font-bold' : 'text-emerald-400'">
                {{ health.consecutiveFailures }}
              </div>
            </div>
          </div>

          <!-- Active Incidents Banner if any -->
          @if (health.incidents.length > 0) {
            <div class="space-y-2">
              <div class="text-xs font-semibold text-rose-400 uppercase tracking-wider flex items-center gap-1.5">
                <svg class="w-4 h-4 text-rose-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>
                </svg>
                <span>Active Reliability Incidents ({{ health.incidents.length }})</span>
              </div>
              <app-incident-alerts-banner [incidents]="health.incidents"></app-incident-alerts-banner>
            </div>
          }

          <!-- 24-Hour Hourly Reliability Timeline Chart -->
          <div class="p-4 rounded-xl bg-surface-900/60 border border-surface-800 space-y-3 font-sans">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-surface-200 uppercase tracking-wider">24-Hour Health & Volume Timeline</span>
              <span class="text-[11px] text-surface-400 font-mono">Hourly slots</span>
            </div>

            <div class="h-32 flex items-end gap-1.5 pt-4 pb-1 border-b border-surface-800 overflow-x-auto">
              @for (bucket of health.hourlyBuckets; track bucket.timestamp) {
                <div class="flex-1 min-w-[12px] flex flex-col items-center gap-1 group relative h-full justify-end">
                  <!-- Bar stack -->
                  <div
                    class="w-full rounded-t transition-all"
                    [ngClass]="getBucketBarColor(bucket)"
                    [style.height.%]="getBucketHeight(bucket, health)">
                  </div>

                  <!-- Tooltip -->
                  <div class="opacity-0 group-hover:opacity-100 pointer-events-none absolute bottom-full mb-2 z-50 bg-surface-950 border border-surface-700 rounded-lg p-2 text-[10px] font-mono shadow-xl whitespace-nowrap transition-opacity">
                    <div class="font-bold text-white">{{ bucket.timestamp | date:'HH:mm' }}</div>
                    <div class="text-emerald-400">{{ bucket.successCount }} success</div>
                    @if (bucket.failedCount > 0) {
                      <div class="text-rose-400">{{ bucket.failedCount }} failed</div>
                    }
                    <div class="text-surface-400">Score: {{ bucket.healthScore }}%</div>
                    <div class="text-surface-400">Avg Latency: {{ bucket.averageLatencyMs }}ms</div>
                  </div>
                </div>
              }
            </div>

            <div class="flex items-center justify-between text-[10px] font-mono text-surface-500">
              <span>24h ago</span>
              <span>12h ago</span>
              <span>Now</span>
            </div>
          </div>

          <!-- Latency Quantiles Breakdown Card -->
          <app-latency-quantiles-card [latencies]="health.latencies"></app-latency-quantiles-card>

          <!-- Failure diagnostics if available -->
          @if (health.lastFailureReason) {
            <div class="p-3 bg-surface-950 border border-surface-800 rounded-xl space-y-1 text-xs">
              <div class="text-[10px] font-semibold text-surface-500 uppercase tracking-wider">Last Recorded Failure Reason</div>
              <div class="font-mono text-rose-300 break-all">{{ health.lastFailureReason }}</div>
              @if (health.lastDeliveryAt) {
                <div class="text-[10px] font-mono text-surface-500">At: {{ health.lastDeliveryAt | date:'medium' }}</div>
              }
            </div>
          }
        </div>
      }

      <!-- Drawer Footer -->
      <div slot="footer" class="flex items-center justify-between w-full">
        <a
          [routerLink]="['/payloads']"
          (click)="onClose()"
          class="px-3 py-1.5 bg-brand-600/20 hover:bg-brand-600/30 text-brand-300 border border-brand-500/30 text-xs font-semibold rounded-lg transition-colors inline-flex items-center gap-1.5">
          <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4"/>
          </svg>
          <span>Open in Payload Lab</span>
        </a>

        <button
          (click)="onClose()"
          type="button"
          class="px-4 py-1.5 bg-surface-900 border border-surface-700 hover:bg-surface-800 text-surface-300 text-xs font-medium rounded-lg transition-colors">
          Close
        </button>
      </div>
    </app-slide-over>
  `
})
export class EndpointHealthDrawerComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly endpointService = inject(EndpointService);

  readonly isOpen = input<boolean>(false);
  readonly endpointId = input<string | null>(null);
  readonly targetUrl = input<string | null>(null);

  readonly closed = output<void>();

  readonly isLoading = signal<boolean>(false);
  readonly errorState = signal<string | null>(null);
  readonly healthDetail = signal<EndpointHealth | null>(null);

  constructor() {
    effect(() => {
      const open = this.isOpen();
      const id = this.endpointId();
      if (open && id) {
        this.loadHealth(id);
      }
    });
  }

  loadHealth(id: string): void {
    this.isLoading.set(true);
    this.errorState.set(null);

    this.endpointService.getEndpointHealth(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => {
        this.healthDetail.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load endpoint health:', err);
        this.errorState.set(err.error?.detail || 'Failed to load health metrics.');
        this.isLoading.set(false);
      }
    });
  }

  retryLoad(): void {
    const id = this.endpointId();
    if (id) this.loadHealth(id);
  }

  getBucketHeight(bucket: any, health: EndpointHealth): number {
    if (bucket.totalDeliveries === 0) return 8;
    const max = Math.max(...health.hourlyBuckets.map(b => b.totalDeliveries), 1);
    return Math.max(12, Math.round((bucket.totalDeliveries / max) * 100));
  }

  getBucketBarColor(bucket: any): string {
    if (bucket.totalDeliveries === 0) return 'bg-surface-800';
    if (bucket.healthScore >= 90) return 'bg-emerald-500 hover:bg-emerald-400';
    if (bucket.healthScore >= 60) return 'bg-amber-500 hover:bg-amber-400';
    return 'bg-rose-500 hover:bg-rose-400';
  }

  onClose(): void {
    this.closed.emit();
  }
}
