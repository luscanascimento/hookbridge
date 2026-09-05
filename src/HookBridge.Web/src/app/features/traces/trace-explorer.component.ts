import { Component, OnInit, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { StatusBadgeComponent } from '../../shared/components/ui/status-badge.component';
import { SkeletonLoaderComponent } from '../../shared/components/ui/skeleton-loader.component';
import { ButtonComponent } from '../../shared/components/ui/button.component';
import { CodeViewerComponent } from '../../shared/components/ui/code-viewer.component';
import { TabGroupComponent } from '../../shared/components/ui/tab-group.component';
import { TabComponent } from '../../shared/components/ui/tab.component';
import { TraceService } from '../../core/services/trace.service';
import { ToastService } from '../../shared/components/ui/toast/toast.service';
import { TraceSummary, TraceDetail, TraceSpan, TraceQueryParams } from '../../shared/models/trace.models';
import { PagedList } from '../../shared/models/control-plane.models';

@Component({
  selector: 'app-trace-explorer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    StatusBadgeComponent,
    SkeletonLoaderComponent,
    ButtonComponent,
    CodeViewerComponent,
    TabGroupComponent,
    TabComponent
  ],
  template: `
    <div class="space-y-6">
      
      <!-- Topbar / Title -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <div class="flex items-center gap-2.5">
            <h1 class="text-xl font-bold tracking-tight text-white">Distributed Trace Explorer</h1>
            <span class="px-2 py-0.5 rounded-full text-[10px] font-mono bg-brand-500/20 text-brand-300 border border-brand-500/30">
              W3C TraceContext + Distributed DAG
            </span>
          </div>
          <p class="text-xs text-surface-400 mt-0.5">
            End-to-end trace correlation visualizer across Gateway Ingestion, Transactional Outbox, RabbitMQ, Workers, and Outbound HTTP Dispatches
          </p>
        </div>

        <div class="flex items-center gap-2">
          <button
            (click)="loadTraces()"
            [disabled]="isLoadingTraces()"
            class="px-3 py-1.5 bg-surface-900 border border-surface-700 hover:bg-surface-800 text-surface-300 hover:text-white rounded-lg text-xs font-medium transition-colors flex items-center gap-1.5">
            <svg class="w-3.5 h-3.5" [ngClass]="{'animate-spin': isLoadingTraces()}" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
            </svg>
            <span>Refresh</span>
          </button>
        </div>
      </div>

      <!-- Filters Bar -->
      <div class="p-3.5 bg-surface-900/90 border border-surface-800 rounded-xl flex flex-wrap items-center justify-between gap-3 text-xs">
        <div class="flex flex-wrap items-center gap-3 flex-1 min-w-[280px]">
          <!-- Search Input -->
          <div class="relative flex-1 min-w-[200px]">
            <input
              type="text"
              [(ngModel)]="searchQuery"
              (keyup.enter)="onSearch()"
              placeholder="Search by Trace ID, Correlation ID, Delivery ID or Event Type..."
              class="w-full pl-8 pr-3 py-1.5 bg-surface-950 border border-surface-800 rounded-lg text-white font-mono text-xs focus:border-brand-500 focus:outline-none placeholder:text-surface-600" />
            <svg class="w-3.5 h-3.5 text-surface-500 absolute left-2.5 top-2.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
            </svg>
          </div>

          <!-- Status Filter -->
          <select
            [(ngModel)]="statusFilter"
            (change)="onSearch()"
            class="px-2.5 py-1.5 bg-surface-950 border border-surface-800 rounded-lg text-surface-300 text-xs focus:border-brand-500 focus:outline-none">
            <option value="">All Statuses</option>
            <option value="Success">Success</option>
            <option value="Failed">Failed</option>
            <option value="DeadLettered">DeadLettered</option>
            <option value="Pending">Pending</option>
          </select>

          <!-- Time Range -->
          <select
            [(ngModel)]="timeRange"
            (change)="onTimeRangeChange()"
            class="px-2.5 py-1.5 bg-surface-950 border border-surface-800 rounded-lg text-surface-300 text-xs focus:border-brand-500 focus:outline-none">
            <option value="all">All Time</option>
            <option value="15m">Past 15 minutes</option>
            <option value="1h">Past 1 hour</option>
            <option value="24h">Past 24 hours</option>
            <option value="7d">Past 7 days</option>
          </select>
        </div>

        <div class="flex items-center gap-2">
          <button
            (click)="resetFilters()"
            class="px-2.5 py-1 text-surface-400 hover:text-white transition-colors">
            Reset
          </button>
          <app-button
            variant="secondary"
            size="sm"
            (clicked)="onSearch()">
            Search
          </app-button>
        </div>
      </div>

      <!-- Main Master-Detail Grid -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        
        <!-- Left Column: Trace List (4 cols) -->
        <div class="lg:col-span-4 bg-surface-900/90 border border-surface-800 rounded-xl overflow-hidden shadow-xl flex flex-col h-[780px]">
          
          <div class="p-3.5 border-b border-surface-800 bg-surface-950/60 flex items-center justify-between text-xs">
            <span class="font-semibold text-surface-300 uppercase tracking-wider text-[11px]">
              Traces ({{ totalTracesCount() }})
            </span>
            <span class="text-surface-500 font-mono text-[10px]">
              Page {{ currentPage() }} of {{ totalPages() }}
            </span>
          </div>

          <!-- Trace Items List -->
          <div class="flex-1 overflow-y-auto divide-y divide-surface-800/60">
            @if (isLoadingTraces()) {
              <div class="p-4 space-y-3">
                <app-skeleton-loader customClass="h-16 w-full"></app-skeleton-loader>
                <app-skeleton-loader customClass="h-16 w-full"></app-skeleton-loader>
                <app-skeleton-loader customClass="h-16 w-full"></app-skeleton-loader>
                <app-skeleton-loader customClass="h-16 w-full"></app-skeleton-loader>
              </div>
            } @else if (tracesList().length === 0) {
              <div class="p-12 text-center text-surface-500 text-xs">
                <svg class="w-8 h-8 mx-auto text-surface-600 mb-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M13 10V3L4 14h7v7l9-11h-7z"/>
                </svg>
                <div class="font-medium text-surface-300">No traces found</div>
                <p class="text-surface-500 mt-1">Publish an event to record distributed traces.</p>
              </div>
            } @else {
              @for (t of tracesList(); track t.correlationId) {
                <div
                  (click)="selectTrace(t.correlationId)"
                  class="p-3.5 hover:bg-surface-800/60 transition-colors cursor-pointer text-xs space-y-1.5"
                  [ngClass]="selectedTraceId() === t.correlationId ? 'bg-surface-800/80 border-l-2 border-brand-500' : ''">
                  
                  <div class="flex items-center justify-between gap-2">
                    <span class="font-bold text-white font-mono truncate max-w-[170px]">{{ t.eventType }}</span>
                    <app-status-badge [status]="t.status"></app-status-badge>
                  </div>

                  <div class="text-[11px] font-mono text-surface-400 truncate" [title]="t.correlationId">
                    Corr: {{ t.correlationId }}
                  </div>

                  <div class="flex items-center justify-between text-[10px] text-surface-500 font-mono pt-1">
                    <span class="text-surface-300 bg-surface-950 px-1.5 py-0.5 rounded border border-surface-800">
                      {{ t.totalDurationMs }}ms • {{ t.spanCount }} spans
                    </span>
                    <span>{{ t.initiatedAt | date:'HH:mm:ss.SSS' }}</span>
                  </div>
                </div>
              }
            }
          </div>

          <!-- Pagination Footer -->
          <div class="p-3 border-t border-surface-800 bg-surface-950/60 flex items-center justify-between text-xs">
            <button
              [disabled]="currentPage() <= 1"
              (click)="goToPage(currentPage() - 1)"
              class="px-2.5 py-1 bg-surface-800 hover:bg-surface-700 text-surface-300 rounded disabled:opacity-30 disabled:cursor-not-allowed transition-colors">
              Prev
            </button>
            <span class="text-surface-500 text-[11px] font-mono">Page {{ currentPage() }}</span>
            <button
              [disabled]="currentPage() >= totalPages()"
              (click)="goToPage(currentPage() + 1)"
              class="px-2.5 py-1 bg-surface-800 hover:bg-surface-700 text-surface-300 rounded disabled:opacity-30 disabled:cursor-not-allowed transition-colors">
              Next
            </button>
          </div>
        </div>

        <!-- Right Column: Trace Detail Waterfall (8 cols) -->
        <div class="lg:col-span-8 bg-surface-900/90 border border-surface-800 rounded-xl overflow-hidden shadow-xl min-h-[780px] flex flex-col">
          
          @if (isLoadingDetail()) {
            <div class="p-8 space-y-6">
              <app-skeleton-loader customClass="h-20 w-full"></app-skeleton-loader>
              <app-skeleton-loader customClass="h-64 w-full"></app-skeleton-loader>
              <app-skeleton-loader customClass="h-32 w-full"></app-skeleton-loader>
            </div>
          } @else if (!traceDetail()) {
            <div class="flex-1 flex flex-col items-center justify-center p-16 text-center text-surface-500 text-xs">
              <div class="w-16 h-16 rounded-2xl bg-surface-800/80 border border-surface-700/60 flex items-center justify-center text-brand-400 mb-4 shadow-lg shadow-black">
                <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/>
                </svg>
              </div>
              <div class="text-base font-bold text-white">Select a Trace to Inspect</div>
              <p class="text-surface-400 mt-1 max-w-md">
                Click any trace on the left or search by Correlation ID / Trace ID to visualize the distributed waterfall timing DAG across all microservices and workers.
              </p>
            </div>
          } @else {
            
            <!-- Hero Header Summary -->
            <div class="p-6 border-b border-surface-800 bg-surface-950/80 space-y-4">
              <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div class="flex items-center gap-3">
                  <div class="p-2.5 bg-brand-500/10 border border-brand-500/30 text-brand-400 rounded-xl">
                    <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/>
                    </svg>
                  </div>
                  <div>
                    <div class="flex items-center gap-2">
                      <span class="text-lg font-bold text-white font-mono">{{ traceDetail()!.eventType }}</span>
                      <app-status-badge [status]="traceDetail()!.overallStatus"></app-status-badge>
                    </div>
                    <div class="flex items-center gap-2 mt-0.5 text-xs text-surface-400 font-mono">
                      <span>Trace ID: {{ traceDetail()!.traceId }}</span>
                      <button
                        (click)="copyText(traceDetail()!.traceId, 'traceId')"
                        class="p-0.5 hover:text-white transition-colors">
                        @if (copiedField() === 'traceId') {
                          <span class="text-[10px] text-emerald-400 font-sans">Copied!</span>
                        } @else {
                          <svg class="w-3.5 h-3.5 text-surface-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10a2 2 0 00-2 2v3a2 2 0 002 2h10a2 2 0 002-2v-3a2 2 0 00-2-2z"/>
                          </svg>
                        }
                      </button>
                    </div>
                  </div>
                </div>

                <!-- KPI Badges -->
                <div class="flex items-center gap-2 font-mono text-xs">
                  <div class="px-3 py-1.5 bg-surface-900 border border-surface-800 rounded-lg text-right">
                    <div class="text-[10px] text-surface-500 uppercase">Duration</div>
                    <div class="text-brand-300 font-bold">{{ traceDetail()!.totalDurationMs }} ms</div>
                  </div>
                  <div class="px-3 py-1.5 bg-surface-900 border border-surface-800 rounded-lg text-right">
                    <div class="text-[10px] text-surface-500 uppercase">Total Spans</div>
                    <div class="text-white font-bold">{{ traceDetail()!.spans.length }}</div>
                  </div>
                </div>
              </div>

              <!-- Meta Grid -->
              <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 pt-2 text-xs font-mono">
                <div class="p-2.5 bg-surface-900/60 rounded-lg border border-surface-800">
                  <span class="text-[10px] text-surface-500 block uppercase">Correlation ID</span>
                  <span class="text-surface-200 truncate block" [title]="traceDetail()!.correlationId">
                    {{ traceDetail()!.correlationId }}
                  </span>
                </div>
                <div class="p-2.5 bg-surface-900/60 rounded-lg border border-surface-800">
                  <span class="text-[10px] text-surface-500 block uppercase">Initiated At</span>
                  <span class="text-surface-200 block">
                    {{ traceDetail()!.initiatedAt | date:'HH:mm:ss.SSS' }}
                  </span>
                </div>
                <div class="p-2.5 bg-surface-900/60 rounded-lg border border-surface-800">
                  <span class="text-[10px] text-surface-500 block uppercase">Deliveries</span>
                  <span class="text-surface-200 block">
                    {{ traceDetail()!.deliveries.length }} scheduled
                  </span>
                </div>
                <div class="p-2.5 bg-surface-900/60 rounded-lg border border-surface-800">
                  <span class="text-[10px] text-surface-500 block uppercase">Audit Entries</span>
                  <span class="text-surface-200 block">
                    {{ traceDetail()!.auditLogs.length }} recorded
                  </span>
                </div>
              </div>
            </div>

            <!-- Tabs Section -->
            <div class="p-6 flex-1 overflow-y-auto">
              <app-tab-group>
                
                <!-- Tab 1: Interactive Waterfall DAG -->
                <app-tab id="waterfall" label="Distributed Waterfall DAG" [badge]="traceDetail()!.spans.length">
                  <div class="space-y-6 pt-2">
                    
                    <div class="text-xs text-surface-400">
                      Sequential timing diagram of distributed spans spanning Gateway Ingestion, PostgreSQL Outbox, RabbitMQ Topic Exchange, Consumer Workers, and Outbound HTTP dispatches:
                    </div>

                    <!-- Waterfall Chart -->
                    <div class="space-y-3">
                      @for (span of traceDetail()!.spans; track span.spanId) {
                        <div
                          (click)="selectSpan(span)"
                          class="p-3 bg-surface-950/70 border rounded-xl hover:border-surface-700 transition-all cursor-pointer group"
                          [ngClass]="selectedSpan()?.spanId === span.spanId ? 'border-brand-500 bg-surface-950 shadow-md' : 'border-surface-800/80'">
                          
                          <!-- Span Header -->
                          <div class="flex items-center justify-between text-xs font-mono mb-2">
                            <div class="flex items-center gap-2">
                              <span
                                class="px-2 py-0.5 rounded text-[10px] font-semibold"
                                [ngClass]="getSpanServiceBadgeClasses(span.service)">
                                {{ span.service }}
                              </span>
                              <span class="font-bold text-white group-hover:text-brand-300 transition-colors">
                                {{ span.name }}
                              </span>
                              <span class="text-[10px] text-surface-500">({{ span.kind }})</span>
                            </div>

                            <div class="flex items-center gap-2">
                              <span class="text-[11px] text-surface-300 font-bold">{{ span.durationMs }} ms</span>
                              <span class="text-[10px] text-surface-500">+{{ span.offsetMs }}ms</span>
                              <span
                                class="w-2 h-2 rounded-full"
                                [ngClass]="span.status === 'Ok' ? 'bg-emerald-400' : span.status === 'Error' ? 'bg-rose-500' : 'bg-amber-400'">
                              </span>
                            </div>
                          </div>

                          <!-- Timing Bar Visualizer -->
                          <div class="h-2 bg-surface-900 rounded-full overflow-hidden relative">
                            <div
                              class="h-full rounded-full transition-all duration-500"
                              [ngClass]="getSpanBarColorClasses(span)"
                              [style.margin-left.%]="calculateSpanOffsetPercent(span)"
                              [style.width.%]="calculateSpanWidthPercent(span)">
                            </div>
                          </div>

                          <!-- Expanded Span Attributes & Events -->
                          @if (selectedSpan()?.spanId === span.spanId) {
                            <div class="mt-4 pt-3 border-t border-surface-800/80 space-y-3 animate-fadeIn">
                              <div class="text-[11px] font-semibold text-surface-400 uppercase tracking-wider">Span Attributes</div>
                              
                              <div class="grid grid-cols-1 sm:grid-cols-2 gap-2 text-xs font-mono">
                                @for (attr of objectEntries(span.attributes); track attr[0]) {
                                  <div class="p-2 bg-surface-900 rounded border border-surface-800 flex items-center justify-between">
                                    <span class="text-surface-500 text-[11px]">{{ attr[0] }}</span>
                                    <span class="text-surface-200 font-semibold">{{ attr[1] }}</span>
                                  </div>
                                }
                              </div>

                              @if (span.events && span.events.length > 0) {
                                <div class="pt-2">
                                  <div class="text-[11px] font-semibold text-surface-400 uppercase tracking-wider mb-2">Internal Events</div>
                                  <div class="space-y-1.5">
                                    @for (evt of span.events; track evt.name) {
                                      <div class="flex items-center justify-between p-2 bg-surface-900/60 rounded border border-surface-800/60 text-xs font-mono">
                                        <span class="text-brand-300 font-medium">{{ evt.name }}</span>
                                        <span class="text-surface-500 text-[10px]">{{ evt.timestamp | date:'HH:mm:ss.SSS' }}</span>
                                      </div>
                                    }
                                  </div>
                                </div>
                              }
                            </div>
                          }
                        </div>
                      }
                    </div>
                  </div>
                </app-tab>

                <!-- Tab 2: Correlated Deliveries -->
                <app-tab id="deliveries" label="Correlated Deliveries" [badge]="traceDetail()!.deliveries.length">
                  <div class="space-y-4 pt-2">
                    @for (d of traceDetail()!.deliveries; track d.id) {
                      <div class="p-4 bg-surface-950 rounded-xl border border-surface-800 space-y-3">
                        <div class="flex items-center justify-between">
                          <div class="flex items-center gap-2 font-mono text-xs">
                            <app-status-badge [status]="d.status"></app-status-badge>
                            <span class="font-bold text-white">{{ d.endpointUrl || d.endpointId }}</span>
                          </div>
                          <span class="text-surface-500 text-xs font-mono">{{ d.attemptCount }} attempts</span>
                        </div>

                        <!-- Attempts Breakdown -->
                        <div class="space-y-2 pt-1">
                          @for (att of d.attempts; track att.id) {
                            <div class="p-3 bg-surface-900 rounded-lg border border-surface-800 flex items-center justify-between text-xs font-mono">
                              <div class="flex items-center gap-2">
                                <span class="text-white font-semibold">Attempt #{{ att.attemptNumber }}</span>
                                <span class="px-2 py-0.5 rounded text-[11px]" [ngClass]="att.httpStatusCode === 200 ? 'bg-emerald-500/20 text-emerald-300' : 'bg-rose-500/20 text-rose-300'">
                                  HTTP {{ att.httpStatusCode || 'None' }}
                                </span>
                                <span class="text-surface-400">{{ att.elapsedMs }}ms</span>
                              </div>
                              <span class="text-surface-500">{{ att.executedAt | date:'HH:mm:ss.SSS' }}</span>
                            </div>
                          }
                        </div>
                      </div>
                    }
                  </div>
                </app-tab>

                <!-- Tab 3: Correlated Audit Trail -->
                <app-tab id="audit" label="Audit Trail" [badge]="traceDetail()!.auditLogs.length">
                  <div class="space-y-3 pt-2">
                    @for (log of traceDetail()!.auditLogs; track log.id) {
                      <div class="p-3.5 bg-surface-950 rounded-xl border border-surface-800 flex items-start justify-between gap-4 text-xs font-mono">
                        <div class="space-y-1">
                          <div class="flex items-center gap-2">
                            <span class="font-bold text-brand-300">{{ log.action }}</span>
                            <span class="text-surface-500">•</span>
                            <span class="text-surface-300">{{ log.resourceType }} ({{ log.resourceId }})</span>
                          </div>
                          <div class="text-surface-400 text-[11px] break-all bg-surface-900 p-2 rounded border border-surface-800">
                            {{ log.detailsJson }}
                          </div>
                        </div>
                        <span class="text-surface-500 text-[11px] shrink-0">{{ log.timestamp | date:'HH:mm:ss.SSS' }}</span>
                      </div>
                    }
                  </div>
                </app-tab>

                <!-- Tab 4: OpenTelemetry JSON Export -->
                <app-tab id="export" label="Raw Trace JSON">
                  <div class="space-y-3 pt-2">
                    <app-code-viewer
                      [code]="traceDetail()!"
                      language="json"
                      title="OpenTelemetry Correlated Trace Graph">
                    </app-code-viewer>
                  </div>
                </app-tab>

              </app-tab-group>
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class TraceExplorerComponent implements OnInit {
  private readonly traceService = inject(TraceService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly tracesList = signal<TraceSummary[]>([]);
  readonly totalTracesCount = signal<number>(0);
  readonly totalPages = signal<number>(1);
  readonly currentPage = signal<number>(1);
  readonly isLoadingTraces = signal<boolean>(false);

  readonly selectedTraceId = signal<string | null>(null);
  readonly traceDetail = signal<TraceDetail | null>(null);
  readonly isLoadingDetail = signal<boolean>(false);
  readonly selectedSpan = signal<TraceSpan | null>(null);
  readonly copiedField = signal<string | null>(null);

  // Filters
  readonly searchQuery = signal<string>('');
  readonly statusFilter = signal<string>('');
  readonly timeRange = signal<string>('all');
  readonly fromDate = signal<string | null>(null);

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      if (params['traceId'] || params['correlationId']) {
        const id = params['traceId'] || params['correlationId'];
        this.searchQuery.set(id);
        this.selectTrace(id);
      }
    });

    this.loadTraces();
  }

  loadTraces(): void {
    this.isLoadingTraces.set(true);

    const params: TraceQueryParams = {
      query: this.searchQuery().trim() || null,
      status: this.statusFilter() || null,
      fromDate: this.fromDate(),
      page: this.currentPage(),
      pageSize: 20
    };

    this.traceService.getTraces(params).subscribe({
      next: (res: PagedList<TraceSummary>) => {
        this.tracesList.set(res.items || []);
        this.totalTracesCount.set(res.totalCount || 0);
        this.totalPages.set(res.totalPages || 1);
        this.isLoadingTraces.set(false);

        // Auto-select first trace if none selected
        if (!this.selectedTraceId() && res.items && res.items.length > 0) {
          this.selectTrace(res.items[0].correlationId);
        }
      },
      error: (err) => {
        console.error('Failed to load traces:', err);
        this.isLoadingTraces.set(false);
        this.toast.error('Failed to retrieve distributed traces.');
      }
    });
  }

  selectTrace(identifier: string): void {
    this.selectedTraceId.set(identifier);
    this.isLoadingDetail.set(true);
    this.selectedSpan.set(null);

    this.traceService.getTraceDetail(identifier).subscribe({
      next: (detail) => {
        this.traceDetail.set(detail);
        this.isLoadingDetail.set(false);
        if (detail.spans && detail.spans.length > 0) {
          this.selectedSpan.set(detail.spans[0]);
        }
      },
      error: (err) => {
        console.error('Failed to load trace detail:', err);
        this.isLoadingDetail.set(false);
        this.toast.error('Trace details could not be resolved.');
      }
    });
  }

  selectSpan(span: TraceSpan): void {
    if (this.selectedSpan()?.spanId === span.spanId) {
      this.selectedSpan.set(null);
    } else {
      this.selectedSpan.set(span);
    }
  }

  onSearch(): void {
    this.currentPage.set(1);
    this.loadTraces();
  }

  onTimeRangeChange(): void {
    const range = this.timeRange();
    const now = new Date();

    switch (range) {
      case '15m':
        this.fromDate.set(new Date(now.getTime() - 15 * 60 * 1000).toISOString());
        break;
      case '1h':
        this.fromDate.set(new Date(now.getTime() - 60 * 60 * 1000).toISOString());
        break;
      case '24h':
        this.fromDate.set(new Date(now.getTime() - 24 * 60 * 60 * 1000).toISOString());
        break;
      case '7d':
        this.fromDate.set(new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000).toISOString());
        break;
      default:
        this.fromDate.set(null);
        break;
    }

    this.currentPage.set(1);
    this.loadTraces();
  }

  resetFilters(): void {
    this.searchQuery.set('');
    this.statusFilter.set('');
    this.timeRange.set('all');
    this.fromDate.set(null);
    this.currentPage.set(1);
    this.loadTraces();
  }

  goToPage(page: number): void {
    this.currentPage.set(page);
    this.loadTraces();
  }

  calculateSpanOffsetPercent(span: TraceSpan): number {
    const total = this.traceDetail()?.totalDurationMs || 1;
    return Math.min(95, Math.max(0, (span.offsetMs / total) * 100));
  }

  calculateSpanWidthPercent(span: TraceSpan): number {
    const total = this.traceDetail()?.totalDurationMs || 1;
    const raw = (span.durationMs / total) * 100;
    return Math.min(100, Math.max(5, raw));
  }

  getSpanServiceBadgeClasses(service: string): string {
    if (service.includes('Gateway')) return 'bg-brand-500/20 text-brand-300 border border-brand-500/30';
    if (service.includes('EventFlow')) return 'bg-sky-500/20 text-sky-300 border border-sky-500/30';
    if (service.includes('RabbitMQ')) return 'bg-amber-500/20 text-amber-300 border border-amber-500/30';
    if (service.includes('Dispatch') || service.includes('Outbound')) return 'bg-purple-500/20 text-purple-300 border border-purple-500/30';
    return 'bg-surface-800 text-surface-300 border border-surface-700';
  }

  getSpanBarColorClasses(span: TraceSpan): string {
    if (span.status === 'Error') return 'bg-rose-500 shadow-sm shadow-rose-500/40';
    if (span.status === 'Pending') return 'bg-amber-400 shadow-sm shadow-amber-400/40';
    if (span.service.includes('Gateway')) return 'bg-brand-500 shadow-sm shadow-brand-500/40';
    if (span.service.includes('EventFlow')) return 'bg-sky-500 shadow-sm shadow-sky-500/40';
    if (span.service.includes('RabbitMQ')) return 'bg-amber-400 shadow-sm shadow-amber-400/40';
    return 'bg-emerald-400 shadow-sm shadow-emerald-400/40';
  }

  objectEntries(obj: Record<string, string>): Array<[string, string]> {
    return Object.entries(obj || {});
  }

  async copyText(text: string, field: string): Promise<void> {
    if (!text) return;
    try {
      await navigator.clipboard.writeText(text);
      this.copiedField.set(field);
      setTimeout(() => this.copiedField.set(null), 2000);
    } catch {
      // Ignored
    }
  }
}
