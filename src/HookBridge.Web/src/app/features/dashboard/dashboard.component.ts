import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/services/auth.service';
import { SignalRService } from '../../core/signalr/services/signalr.service';
import { DeliveryService } from '../../core/services/delivery.service';
import { EndpointService } from '../../core/services/endpoint.service';
import { ToastService } from '../../shared/components/ui/toast/toast.service';
import {
  MetricCardComponent,
  StatusBadgeComponent,
  SkeletonLoaderComponent,
  ButtonComponent,
  SlideOverComponent,
  CodeViewerComponent
} from '../../shared/components';
import {
  DeliveryStats,
  Endpoint,
  TimeSeriesBucket
} from '../../shared/models/control-plane.models';
import { RealtimeDeliveryEvent } from '../../core/signalr/models/signalr.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    DatePipe,
    MetricCardComponent,
    StatusBadgeComponent,
    SkeletonLoaderComponent,
    ButtonComponent,
    SlideOverComponent,
    CodeViewerComponent
  ],
  template: `
    <div class="space-y-6 pb-12">
      <!-- Top Title & Tenant Info Header -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <div class="flex items-center gap-2.5">
            <h1 class="text-xl font-bold tracking-tight text-white">Executive Control Plane</h1>
            <span class="px-2 py-0.5 rounded-full text-[10px] font-mono font-semibold bg-brand-950 border border-brand-800/80 text-brand-300">
              Live Gateway
            </span>
          </div>
          <p class="text-xs text-surface-400 mt-1">
            Realtime delivery analytics, latency distribution and system reliability for
            <span class="text-surface-200 font-medium font-mono">{{ auth.tenantIdentifier() }}</span>
          </p>
        </div>

        <!-- Action Controls -->
        <div class="flex items-center gap-2.5 flex-wrap">
          <app-button
            variant="secondary"
            size="sm"
            [loading]="isLoading()"
            (clicked)="refreshAll()">
            <svg slot="icon-left" class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
            </svg>
            Refresh
          </app-button>

          @if ((stats()?.deadLetteredDeliveries ?? 0) > 0) {
            <app-button
              variant="danger"
              size="sm"
              [loading]="isReplayingDlq()"
              (clicked)="replayAllDlq()">
              <svg slot="icon-left" class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
              </svg>
              Replay DLQ ({{ stats()?.deadLetteredDeliveries }})
            </app-button>
          }

          <a routerLink="/deliveries">
            <app-button variant="primary" size="sm">
              <span>View All Deliveries</span>
              <svg slot="icon-right" class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/>
              </svg>
            </app-button>
          </a>
        </div>
      </div>

      <!-- KPI Executive Metrics Cards (4 Columns) -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <!-- 1. Total Deliveries -->
        <app-metric-card
          label="Total Deliveries"
          [value]="formattedTotalDeliveries()"
          unit="dispatches"
          delta="+100%"
          trend="up"
          period="all-time"
          [sparklineData]="volumeSparkline()"
          [loading]="isLoading()">
        </app-metric-card>

        <!-- 2. Overall Success Rate -->
        <app-metric-card
          label="Success Rate"
          [value]="formattedSuccessRate()"
          unit="SLA 99.9%"
          [delta]="(stats()?.successRatePercentage ?? 100) >= 99 ? 'Healthy' : 'Degraded'"
          [trend]="(stats()?.successRatePercentage ?? 100) >= 99 ? 'up' : 'down'"
          period="target"
          [sparklineData]="successRateSparkline()"
          [loading]="isLoading()">
        </app-metric-card>

        <!-- 3. Average Latency -->
        <app-metric-card
          label="Avg Latency"
          [value]="formattedAvgLatency()"
          unit="ms"
          [delta]="(stats()?.averageLatencyMs ?? 0) < 300 ? '< 300ms SLA' : 'High Latency'"
          [trend]="(stats()?.averageLatencyMs ?? 0) < 300 ? 'up' : 'down'"
          period="p95 SLA"
          [sparklineData]="latencySparkline()"
          [loading]="isLoading()">
        </app-metric-card>

        <!-- 4. Dead Letter Queue -->
        <app-metric-card
          label="Dead Letter Queue"
          [value]="formattedDlqCount()"
          unit="exhausted"
          [delta]="(stats()?.deadLetteredDeliveries ?? 0) === 0 ? 'Zero DLQ' : 'Requires Replay'"
          [trend]="(stats()?.deadLetteredDeliveries ?? 0) === 0 ? 'up' : 'down'"
          period="status"
          [sparklineData]="dlqSparkline()"
          [loading]="isLoading()">
        </app-metric-card>
      </div>

      <!-- Charts & Health Breakdown Section -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- 24-Hour Delivery Volume & Distribution Chart (2 cols) -->
        <div class="lg:col-span-2 bg-surface-900/80 border border-surface-800 rounded-xl p-5 shadow-sm flex flex-col justify-between">
          <div class="flex items-center justify-between border-b border-surface-800/80 pb-4">
            <div>
              <h2 class="text-sm font-semibold text-white tracking-tight">Delivery Throughput (24 Hours)</h2>
              <p class="text-xs text-surface-400 mt-0.5">Hourly distribution of successful, failed, and deadlettered webhooks</p>
            </div>
            <!-- Legend -->
            <div class="flex items-center gap-3 text-[11px] font-sans">
              <span class="inline-flex items-center gap-1.5 text-emerald-400">
                <span class="w-2 h-2 rounded-full bg-emerald-400"></span> Success
              </span>
              <span class="inline-flex items-center gap-1.5 text-rose-400">
                <span class="w-2 h-2 rounded-full bg-rose-400"></span> Failed
              </span>
              <span class="inline-flex items-center gap-1.5 text-amber-400">
                <span class="w-2 h-2 rounded-full bg-amber-400"></span> DeadLettered
              </span>
            </div>
          </div>

          <!-- SVG Hourly Bar Chart -->
          <div class="mt-4 pt-2">
            @if (isLoading()) {
              <div class="h-44 flex items-center justify-center">
                <app-skeleton-loader customClass="h-36 w-full"></app-skeleton-loader>
              </div>
            } @else if (!hasTraffic24h()) {
              <div class="h-44 flex flex-col items-center justify-center text-center p-6 bg-surface-950/40 rounded-xl border border-surface-800/50">
                <svg class="w-8 h-8 text-surface-600 mb-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/>
                </svg>
                <p class="text-xs text-surface-400 font-medium">No webhook dispatches recorded in the last 24 hours</p>
                <p class="text-[11px] text-surface-500 mt-0.5">Events published through EventFlow will appear in this chart</p>
              </div>
            } @else {
              <!-- Bar Chart -->
              <div class="h-44 flex items-end gap-1.5 pt-4 px-1">
                @for (bucket of timeSeriesBuckets(); track bucket.timestamp) {
                  <div class="flex-1 flex flex-col items-center h-full justify-end group relative">
                    <!-- Tooltip -->
                    <div class="absolute bottom-full mb-2 hidden group-hover:flex flex-col bg-surface-950 border border-surface-700 px-2.5 py-1.5 rounded-lg shadow-xl text-[10px] whitespace-nowrap z-20 pointer-events-none">
                      <span class="font-mono text-surface-300 font-semibold">{{ bucket.timestamp | date:'HH:mm' }}</span>
                      <span class="text-emerald-400">Success: {{ bucket.success }}</span>
                      <span class="text-rose-400">Failed: {{ bucket.failed }}</span>
                      @if (bucket.deadLettered > 0) {
                        <span class="text-amber-400">DLQ: {{ bucket.deadLettered }}</span>
                      }
                      <span class="text-surface-400">Avg: {{ bucket.avgLatencyMs }}ms</span>
                    </div>

                    <!-- Stacked Bar Column -->
                    <div class="w-full bg-surface-800/40 rounded-t overflow-hidden flex flex-col justify-end" [style.height.%]="getBarHeightPercent(bucket.total)">
                      @if (bucket.deadLettered > 0) {
                        <div class="w-full bg-amber-500" [style.height.%]="(bucket.deadLettered / bucket.total) * 100"></div>
                      }
                      @if (bucket.failed > 0) {
                        <div class="w-full bg-rose-500" [style.height.%]="(bucket.failed / bucket.total) * 100"></div>
                      }
                      @if (bucket.success > 0) {
                        <div class="w-full bg-emerald-500" [style.height.%]="(bucket.success / bucket.total) * 100"></div>
                      }
                    </div>

                    <!-- X-Axis Label -->
                    <span class="text-[9px] font-mono text-surface-500 mt-1 truncate">
                      {{ bucket.timestamp | date:'HH' }}h
                    </span>
                  </div>
                }
              </div>
            }
          </div>
        </div>

        <!-- Endpoints & System Health Card (1 col) -->
        <div class="bg-surface-900/80 border border-surface-800 rounded-xl p-5 shadow-sm flex flex-col justify-between">
          <div>
            <div class="flex items-center justify-between border-b border-surface-800/80 pb-3">
              <h2 class="text-sm font-semibold text-white tracking-tight">Active Endpoints</h2>
              <a routerLink="/endpoints" class="text-xs text-brand-400 hover:text-brand-300 transition-colors">Manage &rarr;</a>
            </div>

            <div class="mt-4 space-y-3">
              @if (isLoadingEndpoints()) {
                @for (i of [1,2,3]; track i) {
                  <app-skeleton-loader customClass="h-12 w-full"></app-skeleton-loader>
                }
              } @else if (endpoints().length === 0) {
                <div class="p-6 text-center text-xs text-surface-400">
                  <p>No endpoints configured yet.</p>
                  <a routerLink="/endpoints" class="text-brand-400 hover:underline mt-2 inline-block">Create first endpoint</a>
                </div>
              } @else {
                @for (ep of endpoints().slice(0, 4); track ep.id) {
                  <div class="p-2.5 rounded-lg bg-surface-950/60 border border-surface-800/80 flex items-center justify-between gap-3">
                    <div class="min-w-0 flex-1">
                      <div class="flex items-center gap-2">
                        <span class="w-2 h-2 rounded-full" [ngClass]="ep.status === 'Active' ? 'bg-emerald-400' : 'bg-surface-500'"></span>
                        <span class="text-xs font-mono font-medium text-white truncate">{{ ep.targetUrl }}</span>
                      </div>
                      <div class="text-[10px] text-surface-400 mt-0.5 flex items-center gap-2">
                        <span>Rate: {{ ep.rateLimitPerMinute }}/min</span>
                        <span>&bull;</span>
                        <span>Timeout: {{ ep.timeoutSeconds }}s</span>
                      </div>
                    </div>
                    <app-status-badge [status]="ep.status"></app-status-badge>
                  </div>
                }
              }
            </div>
          </div>

          <!-- Bottom Summary Badge -->
          <div class="mt-4 pt-3 border-t border-surface-800/60 flex items-center justify-between text-xs text-surface-400">
            <span>Total Registered</span>
            <span class="font-mono font-semibold text-white">{{ endpoints().length }} Endpoints</span>
          </div>
        </div>
      </div>

      <!-- Live SignalR Delivery Stream Ticker & Inspector -->
      <div class="bg-surface-900/80 border border-surface-800 rounded-xl overflow-hidden shadow-lg shadow-black/20">
        <!-- Live Header Bar -->
        <div class="p-4 border-b border-surface-800 flex items-center justify-between bg-surface-950/60">
          <div class="flex items-center gap-2.5">
            <span class="relative flex h-2.5 w-2.5">
              <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-brand-400 opacity-75"></span>
              <span class="relative inline-flex rounded-full h-2.5 w-2.5 bg-brand-500"></span>
            </span>
            <div>
              <div class="flex items-center gap-2">
                <h2 class="text-sm font-semibold text-white tracking-tight">Realtime Delivery Stream</h2>
                <span class="text-[10px] font-mono bg-surface-800 text-brand-300 px-2 py-0.5 rounded border border-surface-700">
                  {{ signalR.events().length }} buffered
                </span>
              </div>
              <p class="text-[11px] text-surface-400">Incoming webhook events pushed live via SignalR tenant channel</p>
            </div>
          </div>

          <div class="flex items-center gap-2">
            @if (signalR.events().length > 0) {
              <app-button variant="ghost" size="sm" (clicked)="signalR.clearEvents()">
                Clear buffer
              </app-button>
            }
          </div>
        </div>

        <!-- Live Events Table / Feed -->
        <div class="divide-y divide-surface-800/60 max-h-[420px] overflow-y-auto font-sans text-xs">
          @if (signalR.events().length === 0) {
            <div class="p-10 text-center flex flex-col items-center justify-center">
              <div class="w-10 h-10 rounded-xl bg-surface-800/60 border border-surface-700/60 flex items-center justify-center text-surface-500 mb-2">
                <svg class="w-5 h-5 text-brand-400 animate-pulse" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/>
                </svg>
              </div>
              <h3 class="text-xs font-semibold text-white">Listening for live webhook events...</h3>
              <p class="text-[11px] text-surface-400 mt-1 max-w-sm">
                As soon as an event is published to EventFlow and dispatched to subscribers, it will appear here in real time.
              </p>
            </div>
          } @else {
            @for (event of signalR.events(); track event.deliveryId + '-' + event.timestamp) {
              <div
                (click)="inspectEvent(event)"
                class="p-3.5 hover:bg-surface-800/50 flex items-center justify-between gap-4 transition-colors cursor-pointer group">
                <div class="flex items-center gap-3 min-w-0">
                  <app-status-badge [status]="event.status"></app-status-badge>
                  <span class="text-white font-medium font-mono text-xs group-hover:text-brand-300 transition-colors truncate">
                    {{ event.eventName }}
                  </span>
                  <span class="text-surface-500 font-mono text-[11px] hidden sm:inline truncate max-w-xs">
                    {{ event.deliveryId }}
                  </span>
                </div>

                <div class="flex items-center gap-4 shrink-0 font-mono text-[11px]">
                  @if (event.attempt) {
                    <span
                      class="px-2 py-0.5 rounded text-[10px] font-bold"
                      [ngClass]="event.attempt.httpStatusCode && event.attempt.httpStatusCode >= 200 && event.attempt.httpStatusCode < 300 ? 'bg-emerald-950 text-emerald-300 border border-emerald-800/60' : 'bg-rose-950 text-rose-300 border border-rose-800/60'">
                      HTTP {{ event.attempt.httpStatusCode }}
                    </span>
                    <span class="text-surface-300">{{ event.attempt.elapsedMs }}ms</span>
                  }
                  <span class="text-surface-500">{{ event.timestamp | date:'HH:mm:ss' }}</span>
                  <span class="text-surface-400 group-hover:text-white transition-colors">&rarr;</span>
                </div>
              </div>
            }
          }
        </div>
      </div>

      <!-- Slide-Over Drawer for Event / Delivery Inspection -->
      <app-slide-over
        [isOpen]="isInspectorOpen()"
        title="Delivery Inspector"
        [subtitle]="selectedEvent()?.deliveryId ?? ''"
        width="xl"
        (closed)="closeInspector()">

        @if (selectedEvent(); as ev) {
          <div class="space-y-5">
            <!-- Header Summary Card -->
            <div class="p-4 rounded-xl bg-surface-950 border border-surface-800 space-y-3">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="text-xs font-semibold text-surface-400">Event:</span>
                  <span class="text-xs font-mono font-bold text-white">{{ ev.eventName }}</span>
                </div>
                <app-status-badge [status]="ev.status"></app-status-badge>
              </div>

              <div class="grid grid-cols-2 gap-3 text-xs pt-2 border-t border-surface-800/60 font-mono">
                <div>
                  <span class="text-surface-500 block text-[10px] uppercase">Delivery ID</span>
                  <span class="text-surface-200 truncate block">{{ ev.deliveryId }}</span>
                </div>
                <div>
                  <span class="text-surface-500 block text-[10px] uppercase">Endpoint ID</span>
                  <span class="text-surface-200 truncate block">{{ ev.endpointId }}</span>
                </div>
              </div>
            </div>

            <!-- Execution Attempt Details -->
            @if (ev.attempt; as att) {
              <div class="space-y-3">
                <h4 class="text-xs font-semibold text-white tracking-wide uppercase">Latest Transmission Attempt</h4>
                <div class="grid grid-cols-3 gap-2 text-xs font-mono">
                  <div class="p-2.5 rounded-lg bg-surface-950 border border-surface-800">
                    <span class="text-surface-500 block text-[10px]">HTTP Status</span>
                    <span class="font-bold" [ngClass]="att.httpStatusCode && att.httpStatusCode >= 200 && att.httpStatusCode < 300 ? 'text-emerald-400' : 'text-rose-400'">
                      {{ att.httpStatusCode }}
                    </span>
                  </div>
                  <div class="p-2.5 rounded-lg bg-surface-950 border border-surface-800">
                    <span class="text-surface-500 block text-[10px]">Latency</span>
                    <span class="text-white font-bold">{{ att.elapsedMs }} ms</span>
                  </div>
                  <div class="p-2.5 rounded-lg bg-surface-950 border border-surface-800">
                    <span class="text-surface-500 block text-[10px]">Attempt #</span>
                    <span class="text-white font-bold">{{ att.attemptNumber }}</span>
                  </div>
                </div>

                @if (att.errorMessage) {
                  <div class="p-3 rounded-lg bg-rose-950/40 border border-rose-800/80 text-xs text-rose-300">
                    <span class="font-semibold block mb-0.5">Error Message:</span>
                    <span class="font-mono text-[11px]">{{ att.errorMessage }}</span>
                  </div>
                }

                @if (att.requestBody) {
                  <div>
                    <span class="text-xs font-semibold text-surface-300 block mb-1">Dispatched Payload</span>
                    <app-code-viewer [code]="att.requestBody" language="json" title="Payload Body"></app-code-viewer>
                  </div>
                }
              </div>
            }

            <!-- Action Controls in Drawer -->
            <div class="pt-4 border-t border-surface-800 flex items-center justify-end gap-3">
              <app-button
                variant="primary"
                size="sm"
                [loading]="isReplayingSingle()"
                (clicked)="replayCurrentEvent(ev.deliveryId)">
                <svg slot="icon-left" class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
                </svg>
                Replay Delivery
              </app-button>
            </div>
          </div>
        }
      </app-slide-over>
    </div>
  `
})
export class DashboardComponent implements OnInit {
  readonly auth = inject(AuthService);
  readonly signalR = inject(SignalRService);
  private readonly deliveryService = inject(DeliveryService);
  private readonly endpointService = inject(EndpointService);
  private readonly toast = inject(ToastService);

  readonly stats = signal<DeliveryStats | null>(null);
  readonly endpoints = signal<Endpoint[]>([]);
  readonly isLoading = signal<boolean>(true);
  readonly isLoadingEndpoints = signal<boolean>(true);

  readonly isInspectorOpen = signal<boolean>(false);
  readonly selectedEvent = signal<RealtimeDeliveryEvent | null>(null);

  readonly isReplayingDlq = signal<boolean>(false);
  readonly isReplayingSingle = signal<boolean>(false);

  readonly formattedTotalDeliveries = computed(() => {
    const s = this.stats();
    return s ? s.totalDeliveries.toLocaleString() : '0';
  });

  readonly formattedSuccessRate = computed(() => {
    const s = this.stats();
    return s ? `${s.successRatePercentage.toFixed(1)}%` : '100.0%';
  });

  readonly formattedAvgLatency = computed(() => {
    const s = this.stats();
    return s ? `${Math.round(s.averageLatencyMs)}` : '0';
  });

  readonly formattedDlqCount = computed(() => {
    const s = this.stats();
    return s ? s.deadLetteredDeliveries.toLocaleString() : '0';
  });

  readonly timeSeriesBuckets = computed(() => this.stats()?.timeSeries ?? []);

  readonly maxBucketTotal = computed(() => {
    const buckets = this.timeSeriesBuckets();
    if (buckets.length === 0) return 1;
    const max = Math.max(...buckets.map(b => b.total));
    return max > 0 ? max : 1;
  });

  readonly hasTraffic24h = computed(() => {
    const buckets = this.timeSeriesBuckets();
    return buckets.some(b => b.total > 0);
  });

  readonly volumeSparkline = computed(() => {
    const buckets = this.timeSeriesBuckets();
    return buckets.length > 0 ? buckets.map(b => b.total) : [0, 0, 0, 0, 0];
  });

  readonly latencySparkline = computed(() => {
    const buckets = this.timeSeriesBuckets();
    return buckets.length > 0 ? buckets.map(b => b.avgLatencyMs) : [0, 0, 0, 0, 0];
  });

  readonly successRateSparkline = computed(() => {
    const buckets = this.timeSeriesBuckets();
    return buckets.length > 0
      ? buckets.map(b => (b.total > 0 ? (b.success / b.total) * 100 : 100))
      : [100, 100, 100];
  });

  readonly dlqSparkline = computed(() => {
    const buckets = this.timeSeriesBuckets();
    return buckets.length > 0 ? buckets.map(b => b.deadLettered) : [0, 0, 0];
  });

  ngOnInit(): void {
    this.refreshAll();
  }

  refreshAll(): void {
    this.fetchStats();
    this.fetchEndpoints();
  }

  fetchStats(): void {
    this.isLoading.set(true);
    this.deliveryService.getStats().subscribe({
      next: (res) => {
        this.stats.set(res);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.toast.error('Failed to load telemetry statistics');
      }
    });
  }

  fetchEndpoints(): void {
    this.isLoadingEndpoints.set(true);
    this.endpointService.getEndpoints().subscribe({
      next: (res) => {
        this.endpoints.set(res);
        this.isLoadingEndpoints.set(false);
      },
      error: () => {
        this.isLoadingEndpoints.set(false);
      }
    });
  }

  getBarHeightPercent(total: number): number {
    if (total === 0) return 4; // minimum visible tick
    const max = this.maxBucketTotal();
    return Math.max(10, Math.round((total / max) * 100));
  }

  inspectEvent(event: RealtimeDeliveryEvent): void {
    this.selectedEvent.set(event);
    this.isInspectorOpen.set(true);
  }

  closeInspector(): void {
    this.isInspectorOpen.set(false);
    this.selectedEvent.set(null);
  }

  replayCurrentEvent(deliveryId: string): void {
    this.isReplayingSingle.set(true);
    this.deliveryService.replayDelivery(deliveryId).subscribe({
      next: () => {
        this.isReplayingSingle.set(false);
        this.toast.success('Delivery replayed successfully via EventFlow');
        this.closeInspector();
        this.fetchStats();
      },
      error: (err) => {
        this.isReplayingSingle.set(false);
        this.toast.error(err?.error?.detail ?? 'Failed to replay delivery');
      }
    });
  }

  replayAllDlq(): void {
    this.isReplayingDlq.set(true);
    this.deliveryService.bulkReplay({ status: 'DeadLettered' as any, maxCount: 100 }).subscribe({
      next: (res) => {
        this.isReplayingDlq.set(false);
        this.toast.success(`Queued ${res.replayedCount} dead-lettered deliveries for replay`);
        this.fetchStats();
      },
      error: (err) => {
        this.isReplayingDlq.set(false);
        this.toast.error(err?.error?.detail ?? 'Failed to replay dead letters');
      }
    });
  }
}
