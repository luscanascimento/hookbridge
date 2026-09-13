import { Component, OnInit, OnDestroy, inject, signal, computed, ChangeDetectionStrategy, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ObservabilityService } from '../../core/services/observability.service';
import {
  ObservabilitySummary,
  MetricInstrument,
  CapturedSpan,
  SyntheticTraceCommand,
  SyntheticTraceResult
} from '../../core/models/observability.models';
import { MetricCardComponent } from '../../shared/components/ui/metric-card.component';
import { TabGroupComponent } from '../../shared/components/ui/tab-group.component';
import { TabComponent } from '../../shared/components/ui/tab.component';
import { ButtonComponent } from '../../shared/components/ui/button.component';
import { CodeViewerComponent } from '../../shared/components/ui/code-viewer.component';
import { SkeletonLoaderComponent } from '../../shared/components/ui/skeleton-loader.component';
import { ToastService } from '../../shared/components/ui/toast/toast.service';

@Component({
  selector: 'app-observability',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    MetricCardComponent,
    TabGroupComponent,
    TabComponent,
    ButtonComponent,
    CodeViewerComponent,
    SkeletonLoaderComponent
  ],
  template: `
    <div class="space-y-6">
      
      <!-- Topbar Header -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <div class="flex items-center gap-2.5">
            <h1 class="text-xl font-bold tracking-tight text-white">OpenTelemetry Observability</h1>
            <span class="px-2 py-0.5 rounded-full text-[10px] font-mono bg-violet-500/20 text-violet-300 border border-violet-500/30">
              OTel SDK 1.18 + Prometheus + W3C
            </span>
          </div>
          <p class="text-xs text-surface-400 mt-0.5">
            Full-stack telemetry plane inspecting distributed traces, live metric instruments, GC runtimes, and Prometheus scrape endpoints
          </p>
        </div>

        <!-- Actions -->
        <div class="flex items-center gap-2">
          <!-- Auto Refresh Switch -->
          <button
            (click)="toggleAutoRefresh()"
            class="px-2.5 py-1.5 rounded-lg text-xs font-mono border transition-colors flex items-center gap-1.5"
            [ngClass]="isAutoRefresh() ? 'bg-emerald-950/60 border-emerald-700/60 text-emerald-300' : 'bg-surface-900 border-surface-700 text-surface-400 hover:text-surface-200'">
            <span class="w-2 h-2 rounded-full" [ngClass]="isAutoRefresh() ? 'bg-emerald-400 animate-pulse' : 'bg-surface-500'"></span>
            <span>Auto (5s)</span>
          </button>

          <!-- Refresh Button -->
          <button
            (click)="loadAll()"
            [disabled]="isLoadingSummary()"
            class="px-3 py-1.5 bg-surface-900 border border-surface-700 hover:bg-surface-800 text-surface-300 hover:text-white rounded-lg text-xs font-medium transition-colors flex items-center gap-1.5">
            <svg class="w-3.5 h-3.5" [ngClass]="{'animate-spin': isLoadingSummary()}" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
            </svg>
            <span>Refresh</span>
          </button>

          <!-- Raw Prometheus Scrape Link -->
          <a
            href="/metrics"
            target="_blank"
            rel="noopener noreferrer"
            class="px-3 py-1.5 bg-brand-600/20 border border-brand-500/40 hover:bg-brand-600/30 text-brand-300 rounded-lg text-xs font-medium transition-colors flex items-center gap-1.5">
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14"/>
            </svg>
            <span>/metrics</span>
          </a>
        </div>
      </div>

      <!-- KPI Metric Cards Grid -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <!-- 1. Process Memory -->
        <app-metric-card
          label="Process Memory"
          [value]="summary() ? summary()!.processWorkingSetMb : '—'"
          unit="MB Working Set"
          icon="⚡"
          [delta]="summary() ? 'Heap: ' + summary()!.processHeapMb + ' MB' : null"
          trend="neutral"
          period="Resident Set"
          [loading]="isLoadingSummary()">
        </app-metric-card>

        <!-- 2. GC Collections -->
        <app-metric-card
          label="GC Collections"
          [value]="summary() ? (summary()!.gcGen0Collections + summary()!.gcGen1Collections + summary()!.gcGen2Collections) : '—'"
          unit="Total Runs"
          icon="🧹"
          [delta]="summary() ? 'G0: ' + summary()!.gcGen0Collections + ' | G1: ' + summary()!.gcGen1Collections + ' | G2: ' + summary()!.gcGen2Collections : null"
          trend="neutral"
          period="CLR Generational"
          [loading]="isLoadingSummary()">
        </app-metric-card>

        <!-- 3. Registered Instruments -->
        <app-metric-card
          label="OpenTelemetry Metrics"
          [value]="instruments().length"
          unit="Active Instruments"
          icon="📊"
          [delta]="summary()?.otlpExporterConfigured ? 'OTLP: Connected' : 'In-Memory Ring'"
          [trend]="summary()?.otlpExporterConfigured ? 'up' : 'neutral'"
          period="Meter: HookBridge.Diagnostics"
          [loading]="isLoadingInstruments()">
        </app-metric-card>

        <!-- 4. Distributed Spans -->
        <app-metric-card
          label="Recorded Spans Buffer"
          [value]="summary() ? summary()!.totalRecordedSpansCount : spans().length"
          unit="Buffered Spans"
          icon="🛰️"
          delta="Ring Capacity: 200"
          trend="neutral"
          period="ActivitySource Tracing"
          [loading]="isLoadingSpans()">
        </app-metric-card>
      </div>

      <!-- Main Tabs Component -->
      <div class="bg-surface-900/90 border border-surface-800 rounded-xl overflow-hidden shadow-xl">
        <div class="p-6">
          <app-tab-group>
            
            <!-- TAB 1: Metric Instruments & Live Values -->
            <app-tab id="instruments" label="Metric Instruments" [badge]="instruments().length">
              <div class="space-y-6 pt-2">
                <!-- Search & Filters -->
                <div class="flex flex-col sm:flex-row items-center justify-between gap-3 text-xs">
                  <div class="relative flex-1 w-full sm:max-w-md">
                    <input
                      type="text"
                      [(ngModel)]="searchInstrument"
                      placeholder="Search metrics (e.g. events, latency, deliveries)..."
                      class="w-full pl-8 pr-3 py-1.5 bg-surface-950 border border-surface-800 rounded-lg text-white font-mono text-xs focus:border-brand-500 focus:outline-none placeholder:text-surface-600" />
                    <svg class="w-3.5 h-3.5 text-surface-500 absolute left-2.5 top-2.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
                    </svg>
                  </div>

                  <div class="flex items-center gap-2">
                    <button
                      (click)="togglePrometheusModal()"
                      class="px-3 py-1.5 bg-surface-800 hover:bg-surface-700 text-surface-200 rounded-lg text-xs font-mono transition-colors flex items-center gap-1.5">
                      <svg class="w-3.5 h-3.5 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>
                      </svg>
                      <span>{{ showPrometheusModal() ? 'Hide Raw Scrape' : 'View Prometheus Exposition' }}</span>
                    </button>
                  </div>
                </div>

                <!-- Raw Prometheus Scrape Text Section -->
                @if (showPrometheusModal()) {
                  <div class="space-y-2 p-4 bg-surface-950 rounded-xl border border-surface-800 animate-fadeIn">
                    <div class="flex items-center justify-between text-xs">
                      <span class="font-mono text-amber-400 font-bold">Standard Prometheus Text 0.0.4 Output</span>
                      <button
                        (click)="copyRawPrometheus()"
                        class="px-2 py-1 bg-surface-900 border border-surface-700 rounded text-[11px] text-surface-300 hover:text-white transition-colors">
                        {{ copiedPrometheus() ? 'Copied!' : 'Copy Exposition' }}
                      </button>
                    </div>
                    <app-code-viewer
                      [code]="rawPrometheusText()"
                      language="plaintext"
                      title="HTTP GET /metrics Response">
                    </app-code-viewer>
                  </div>
                }

                <!-- Instruments Table -->
                <div class="overflow-x-auto border border-surface-800 rounded-xl">
                  <table class="w-full text-left text-xs">
                    <thead class="bg-surface-950/80 text-surface-400 font-mono text-[11px] uppercase border-b border-surface-800">
                      <tr>
                        <th class="p-3">Instrument Name</th>
                        <th class="p-3">Type</th>
                        <th class="p-3">Unit</th>
                        <th class="p-3 text-right">Current Value</th>
                        <th class="p-3">Description</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-surface-800/60 font-mono">
                      @if (isLoadingInstruments()) {
                        <tr>
                          <td colspan="5" class="p-6 text-center text-surface-500">
                            <app-skeleton-loader customClass="h-8 w-full mb-2"></app-skeleton-loader>
                            <app-skeleton-loader customClass="h-8 w-full"></app-skeleton-loader>
                          </td>
                        </tr>
                      } @else if (filteredInstruments().length === 0) {
                        <tr>
                          <td colspan="5" class="p-8 text-center text-surface-500 font-sans">
                            No instruments matching "{{ searchInstrument() }}".
                          </td>
                        </tr>
                      } @else {
                        @for (inst of filteredInstruments(); track inst.name) {
                          <tr class="hover:bg-surface-800/40 transition-colors">
                            <!-- Name -->
                            <td class="p-3 font-bold text-brand-300">
                              <div class="flex items-center gap-1.5">
                                <span class="w-1.5 h-1.5 rounded-full bg-brand-400"></span>
                                <span>{{ inst.name }}</span>
                              </div>
                            </td>

                            <!-- Type -->
                            <td class="p-3">
                              <span
                                class="px-2 py-0.5 rounded text-[10px] font-semibold"
                                [ngClass]="getInstrumentTypeBadgeClasses(inst.type)">
                                {{ inst.type }}
                              </span>
                            </td>

                            <!-- Unit -->
                            <td class="p-3 text-surface-400">{{ inst.unit }}</td>

                            <!-- Value -->
                            <td class="p-3 text-right">
                              <span class="px-2 py-0.5 rounded text-[11px] font-bold bg-surface-950 text-emerald-400 border border-surface-800">
                                {{ inst.currentValue | number }}
                              </span>
                            </td>

                            <!-- Description -->
                            <td class="p-3 text-surface-300 font-sans text-xs max-w-xs truncate" [title]="inst.description">
                              {{ inst.description }}
                            </td>
                          </tr>
                        }
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            </app-tab>

            <!-- TAB 2: Captured Distributed Spans Stream -->
            <app-tab id="spans" label="Recent Captured Spans" [badge]="spans().length">
              <div class="space-y-4 pt-2">
                <!-- Controls -->
                <div class="flex flex-col sm:flex-row items-center justify-between gap-3 text-xs">
                  <div class="relative flex-1 w-full sm:max-w-md">
                    <input
                      type="text"
                      [(ngModel)]="searchSpan"
                      placeholder="Filter spans by operation, trace ID or source..."
                      class="w-full pl-8 pr-3 py-1.5 bg-surface-950 border border-surface-800 rounded-lg text-white font-mono text-xs focus:border-brand-500 focus:outline-none placeholder:text-surface-600" />
                    <svg class="w-3.5 h-3.5 text-surface-500 absolute left-2.5 top-2.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
                    </svg>
                  </div>

                  <div class="flex items-center gap-2">
                    <span class="text-surface-500 font-mono text-[11px]">Buffer Limit:</span>
                    <select
                      [(ngModel)]="spanLimit"
                      (change)="loadSpans()"
                      class="px-2.5 py-1.5 bg-surface-950 border border-surface-800 rounded-lg text-surface-300 font-mono text-xs focus:border-brand-500 focus:outline-none">
                      <option [value]="25">25 Spans</option>
                      <option [value]="50">50 Spans</option>
                      <option [value]="100">100 Spans</option>
                      <option [value]="200">200 Spans</option>
                    </select>
                  </div>
                </div>

                <!-- Spans List -->
                <div class="space-y-2.5">
                  @if (isLoadingSpans()) {
                    <div class="space-y-2">
                      <app-skeleton-loader customClass="h-16 w-full"></app-skeleton-loader>
                      <app-skeleton-loader customClass="h-16 w-full"></app-skeleton-loader>
                      <app-skeleton-loader customClass="h-16 w-full"></app-skeleton-loader>
                    </div>
                  } @else if (filteredSpans().length === 0) {
                    <div class="p-12 text-center text-surface-500 text-xs bg-surface-950/40 rounded-xl border border-surface-800">
                      <svg class="w-8 h-8 mx-auto text-surface-600 mb-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M13 10V3L4 14h7v7l9-11h-7z"/>
                      </svg>
                      <div class="font-medium text-surface-300">No spans recorded yet</div>
                      <p class="text-surface-500 mt-1">Execute a synthetic trace or publish a webhook event to stream OpenTelemetry spans.</p>
                    </div>
                  } @else {
                    @for (span of filteredSpans(); track span.spanId) {
                      <div
                        class="p-3.5 bg-surface-950 rounded-xl border transition-all cursor-pointer"
                        [ngClass]="selectedSpan()?.spanId === span.spanId ? 'border-brand-500 bg-surface-950 shadow-md' : 'border-surface-800 hover:border-surface-700'"
                        (click)="toggleSpanDetails(span)">
                        
                        <!-- Top line -->
                        <div class="flex items-center justify-between gap-3 text-xs font-mono">
                          <div class="flex items-center gap-2">
                            <span
                              class="px-2 py-0.5 rounded text-[10px] font-bold"
                              [ngClass]="span.status === 'Ok' ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30' : span.status === 'Error' ? 'bg-rose-500/20 text-rose-300 border border-rose-500/30' : 'bg-surface-800 text-surface-300 border border-surface-700'">
                              {{ span.status }}
                            </span>
                            <span class="font-bold text-white text-sm">{{ span.operationName }}</span>
                            <span class="text-[11px] text-surface-400 bg-surface-900 px-1.5 py-0.5 rounded border border-surface-800">
                              {{ span.sourceName }}
                            </span>
                          </div>

                          <div class="flex items-center gap-3">
                            <span class="text-surface-300 font-bold bg-surface-900 px-2 py-0.5 rounded border border-surface-800">
                              {{ span.durationMs }} ms
                            </span>
                            <span class="text-surface-500 text-[10px]">{{ span.startTime | date:'HH:mm:ss.SSS' }}</span>
                          </div>
                        </div>

                        <!-- Sub details IDs -->
                        <div class="flex flex-wrap items-center gap-4 mt-2 text-[11px] font-mono text-surface-500">
                          <div class="flex items-center gap-1">
                            <span>Trace:</span>
                            <span class="text-surface-300">{{ span.traceId.slice(0, 16) }}...</span>
                            <button
                              (click)="$event.stopPropagation(); copyText(span.traceId)"
                              title="Copy Trace ID"
                              class="p-0.5 hover:text-white transition-colors">
                              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10a2 2 0 00-2 2v3a2 2 0 002 2h10a2 2 0 002-2v-3a2 2 0 00-2-2z"/>
                              </svg>
                            </button>
                          </div>

                          <div class="flex items-center gap-1">
                            <span>Span:</span>
                            <span class="text-surface-400">{{ span.spanId }}</span>
                          </div>

                          @if (span.parentSpanId) {
                            <div class="flex items-center gap-1">
                              <span>Parent:</span>
                              <span class="text-surface-400">{{ span.parentSpanId }}</span>
                            </div>
                          }

                          <div class="ml-auto text-[10px] text-surface-400">
                            {{ objectKeys(span.tags).length }} tags • {{ objectKeys(span.baggage).length }} baggage
                          </div>
                        </div>

                        <!-- Expandable Attributes Section -->
                        @if (selectedSpan()?.spanId === span.spanId) {
                          <div class="mt-3 pt-3 border-t border-surface-800 space-y-3 animate-fadeIn">
                            <div class="text-[11px] font-semibold text-surface-400 uppercase tracking-wider">Span Semantic Tags</div>
                            <div class="grid grid-cols-1 sm:grid-cols-2 gap-2 text-xs font-mono">
                              @for (entry of objectEntries(span.tags); track entry[0]) {
                                <div class="p-2 bg-surface-900 rounded-lg border border-surface-800 flex items-center justify-between">
                                  <span class="text-surface-400 text-[11px]">{{ entry[0] }}</span>
                                  <span class="text-surface-200 font-semibold truncate max-w-[200px]" [title]="entry[1]">{{ entry[1] }}</span>
                                </div>
                              }
                            </div>

                            @if (objectKeys(span.baggage).length > 0) {
                              <div class="pt-1">
                                <div class="text-[11px] font-semibold text-surface-400 uppercase tracking-wider mb-2">W3C Baggage Items</div>
                                <div class="grid grid-cols-1 sm:grid-cols-2 gap-2 text-xs font-mono">
                                  @for (entry of objectEntries(span.baggage); track entry[0]) {
                                    <div class="p-2 bg-surface-900/60 rounded border border-surface-800/60 flex items-center justify-between">
                                      <span class="text-violet-400 text-[11px]">{{ entry[0] }}</span>
                                      <span class="text-surface-300">{{ entry[1] }}</span>
                                    </div>
                                  }
                                </div>
                              </div>
                            }
                          </div>
                        }
                      </div>
                    }
                  }
                </div>
              </div>
            </app-tab>

            <!-- TAB 3: Synthetic Trace Pipeline Tester -->
            <app-tab id="synthetic" label="Synthetic Pipeline Tester">
              <div class="space-y-6 pt-2">
                <div class="p-4 bg-surface-950 rounded-xl border border-surface-800 space-y-4">
                  <div>
                    <h3 class="text-sm font-bold text-white">Generate End-to-End Synthetic Distributed Trace</h3>
                    <p class="text-xs text-surface-400 mt-0.5">
                      Executes real OpenTelemetry Activity spans through the complete gateway processing chain: Ingestion ➔ HMAC Signing ➔ Outbox Persistence ➔ HTTP Dispatch.
                    </p>
                  </div>

                  <!-- Form Controls -->
                  <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 text-xs font-mono">
                    <div>
                      <label class="block text-surface-400 text-[11px] mb-1 font-semibold uppercase">Event Type</label>
                      <input
                        type="text"
                        [(ngModel)]="syntheticEventType"
                        class="w-full px-3 py-2 bg-surface-900 border border-surface-700 rounded-lg text-white text-xs focus:border-brand-500 focus:outline-none" />
                    </div>

                    <div>
                      <label class="block text-surface-400 text-[11px] mb-1 font-semibold uppercase">Per-Span Delay ({{ syntheticDelayMs }} ms)</label>
                      <input
                        type="range"
                        min="5"
                        max="100"
                        step="5"
                        [(ngModel)]="syntheticDelayMs"
                        class="w-full accent-brand-500 cursor-pointer" />
                    </div>

                    <div class="flex flex-col justify-end">
                      <label class="flex items-center gap-2 cursor-pointer select-none p-2 bg-surface-900 rounded-lg border border-surface-700">
                        <input
                          type="checkbox"
                          [(ngModel)]="syntheticIncludeFailure"
                          class="rounded bg-surface-950 border-surface-700 text-rose-500 focus:ring-0" />
                        <span class="text-xs font-semibold" [ngClass]="syntheticIncludeFailure ? 'text-rose-400' : 'text-surface-300'">
                          Simulate Dispatch Failure (500)
                        </span>
                      </label>
                    </div>
                  </div>

                  <!-- Trigger Button -->
                  <div class="flex justify-end pt-2">
                    <app-button
                      variant="primary"
                      size="sm"
                      [loading]="isGeneratingTrace()"
                      (clicked)="triggerSyntheticTrace()">
                      ⚡ Run Synthetic Pipeline
                    </app-button>
                  </div>
                </div>

                <!-- Synthetic Trace Result DAG Visualizer -->
                @if (syntheticResult()) {
                  <div class="space-y-4 p-5 bg-surface-950 rounded-xl border border-brand-500/40 shadow-xl animate-fadeIn">
                    <!-- Result Summary Header -->
                    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-surface-800">
                      <div class="space-y-1">
                        <div class="flex items-center gap-2">
                          <span class="text-base font-bold text-white font-mono">Trace Generated Successfully</span>
                          <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                            {{ syntheticResult()!.totalSpansGenerated }} Spans
                          </span>
                        </div>
                        <div class="text-xs font-mono text-surface-400 flex items-center gap-2">
                          <span>Trace ID: {{ syntheticResult()!.traceId }}</span>
                          <button
                            (click)="copyText(syntheticResult()!.traceId)"
                            class="text-surface-500 hover:text-white transition-colors">
                            Copy
                          </button>
                        </div>
                      </div>

                      <div class="text-right font-mono text-xs">
                        <div class="text-[10px] text-surface-500 uppercase">Total Execution Time</div>
                        <div class="text-brand-300 font-bold text-base">{{ syntheticResult()!.totalDurationMs }} ms</div>
                      </div>
                    </div>

                    <!-- Waterfall Visualization -->
                    <div class="space-y-3 pt-2">
                      <div class="text-xs font-semibold text-surface-400 uppercase tracking-wider font-mono">
                        Pipeline Execution Waterfall (DAG)
                      </div>

                      @for (span of syntheticResult()!.spans; track span.spanId) {
                        <div class="p-3 bg-surface-900/80 border border-surface-800 rounded-xl space-y-2 font-mono text-xs">
                          <div class="flex items-center justify-between">
                            <div class="flex items-center gap-2">
                              <span class="font-bold text-white">{{ span.operationName }}</span>
                              <span class="text-[10px] text-surface-400 bg-surface-950 px-1.5 py-0.5 rounded border border-surface-800">
                                {{ span.sourceName }}
                              </span>
                            </div>

                            <div class="flex items-center gap-2">
                              <span class="font-bold text-surface-200">{{ span.durationMs }} ms</span>
                              <span
                                class="w-2 h-2 rounded-full"
                                [ngClass]="span.status === 'Ok' ? 'bg-emerald-400' : span.status === 'Error' ? 'bg-rose-500' : 'bg-amber-400'">
                              </span>
                            </div>
                          </div>

                          <!-- Tags pill row -->
                          <div class="flex flex-wrap gap-1.5 pt-1">
                            @for (entry of objectEntries(span.tags); track entry[0]) {
                              <span class="text-[10px] bg-surface-950 text-surface-400 px-2 py-0.5 rounded border border-surface-800">
                                <strong class="text-surface-300">{{ entry[0] }}</strong>: {{ entry[1] }}
                              </span>
                            }
                          </div>
                        </div>
                      }
                    </div>
                  </div>
                }
              </div>
            </app-tab>

            <!-- TAB 4: Collector & W3C Specs -->
            <app-tab id="config" label="Collector & Specs">
              <div class="space-y-6 pt-2 text-xs">
                
                <!-- Environment Info -->
                <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 font-mono">
                  <div class="p-3 bg-surface-950 rounded-xl border border-surface-800">
                    <span class="text-[10px] text-surface-500 uppercase block">Service Name</span>
                    <span class="text-white font-bold">{{ summary()?.serviceName || 'HookBridge.Api' }}</span>
                  </div>
                  <div class="p-3 bg-surface-950 rounded-xl border border-surface-800">
                    <span class="text-[10px] text-surface-500 uppercase block">Service Version</span>
                    <span class="text-white font-bold">{{ summary()?.serviceVersion || '1.0.0' }}</span>
                  </div>
                  <div class="p-3 bg-surface-950 rounded-xl border border-surface-800">
                    <span class="text-[10px] text-surface-500 uppercase block">Environment</span>
                    <span class="text-brand-400 font-bold">{{ summary()?.environment || 'Development' }}</span>
                  </div>
                  <div class="p-3 bg-surface-950 rounded-xl border border-surface-800">
                    <span class="text-[10px] text-surface-500 uppercase block">OTLP Exporter</span>
                    <span class="font-bold" [ngClass]="summary()?.otlpExporterConfigured ? 'text-emerald-400' : 'text-amber-400'">
                      {{ summary()?.otlpExporterConfigured ? (summary()?.otlpEndpoint || 'Enabled') : 'In-Memory Telemetry Ring Buffer' }}
                    </span>
                  </div>
                </div>

                <!-- Specs Card -->
                <div class="p-5 bg-surface-950 rounded-xl border border-surface-800 space-y-3">
                  <h4 class="font-bold text-white text-sm">W3C Distributed Tracing Integration</h4>
                  <p class="text-surface-400 leading-relaxed">
                    HookBridge automatically handles W3C TraceContext standards. Inbound HTTP requests with <code class="text-brand-300 font-mono">traceparent</code> headers are extracted, attached to downstream RabbitMQ message properties, and injected as response headers:
                  </p>
                  
                  <div class="grid grid-cols-1 md:grid-cols-2 gap-3 pt-2 font-mono text-xs">
                    <div class="p-3 bg-surface-900 rounded-lg border border-surface-800">
                      <div class="text-violet-400 font-bold mb-1">X-Trace-Id Header</div>
                      <div class="text-surface-400 text-[11px]">Returned in every API response with the 32-character hex trace identifier.</div>
                    </div>
                    <div class="p-3 bg-surface-900 rounded-lg border border-surface-800">
                      <div class="text-violet-400 font-bold mb-1">traceparent Header</div>
                      <div class="text-surface-400 text-[11px]">Format: <code>00-&lt;trace-id&gt;-&lt;span-id&gt;-01</code> for end-to-end W3C propagation.</div>
                    </div>
                  </div>
                </div>

                <!-- Integration Command Examples -->
                <div class="space-y-2">
                  <div class="font-bold text-white text-xs uppercase tracking-wider">Prometheus Scrape Configuration (prometheus.yml)</div>
                  <app-code-viewer
                    code="scrape_configs:
  - job_name: 'hookbridge'
    scrape_interval: 10s
    metrics_path: '/metrics'
    static_configs:
      - targets: ['localhost:5000']"
                    language="yaml"
                    title="prometheus.yml">
                  </app-code-viewer>
                </div>

              </div>
            </app-tab>

          </app-tab-group>
        </div>
      </div>

    </div>
  `
})
export class ObservabilityComponent implements OnInit, OnDestroy {
  private readonly destroyRef = inject(DestroyRef);
  private readonly obsService = inject(ObservabilityService);
  private readonly toast = inject(ToastService);

  // States
  readonly summary = signal<ObservabilitySummary | null>(null);
  readonly isLoadingSummary = signal<boolean>(false);

  readonly instruments = signal<MetricInstrument[]>([]);
  readonly isLoadingInstruments = signal<boolean>(false);

  readonly spans = signal<CapturedSpan[]>([]);
  readonly isLoadingSpans = signal<boolean>(false);

  readonly rawPrometheusText = signal<string>('');
  readonly showPrometheusModal = signal<boolean>(false);
  readonly copiedPrometheus = signal<boolean>(false);

  // Filters & Search
  readonly searchInstrument = signal<string>('');
  readonly searchSpan = signal<string>('');
  readonly spanLimit = signal<number>(50);
  readonly selectedSpan = signal<CapturedSpan | null>(null);

  // Synthetic Trace Form
  syntheticEventType = 'order.completed';
  syntheticDelayMs = 25;
  syntheticIncludeFailure = false;
  readonly isGeneratingTrace = signal<boolean>(false);
  readonly syntheticResult = signal<SyntheticTraceResult | null>(null);

  // Auto-refresh interval
  readonly isAutoRefresh = signal<boolean>(false);
  private timerId: any = null;

  // Computed Filters
  readonly filteredInstruments = computed(() => {
    const q = this.searchInstrument().trim().toLowerCase();
    const items = this.instruments();
    if (!q) return items;
    return items.filter(i =>
      i.name.toLowerCase().includes(q) ||
      i.description.toLowerCase().includes(q) ||
      i.type.toLowerCase().includes(q)
    );
  });

  readonly filteredSpans = computed(() => {
    const q = this.searchSpan().trim().toLowerCase();
    const items = this.spans();
    if (!q) return items;
    return items.filter(s =>
      s.operationName.toLowerCase().includes(q) ||
      s.sourceName.toLowerCase().includes(q) ||
      s.traceId.toLowerCase().includes(q) ||
      s.spanId.toLowerCase().includes(q)
    );
  });

  ngOnInit(): void {
    this.loadAll();
  }

  ngOnDestroy(): void {
    if (this.timerId) {
      clearInterval(this.timerId);
    }
  }

  loadAll(): void {
    this.loadSummary();
    this.loadInstruments();
    this.loadSpans();
  }

  loadSummary(): void {
    this.isLoadingSummary.set(true);
    this.obsService.getSummary().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => {
        this.summary.set(data);
        this.isLoadingSummary.set(false);
      },
      error: (err) => {
        console.error('Failed to load observability summary:', err);
        this.isLoadingSummary.set(false);
      }
    });
  }

  loadInstruments(): void {
    this.isLoadingInstruments.set(true);
    this.obsService.getInstruments().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => {
        this.instruments.set(data);
        this.isLoadingInstruments.set(false);
      },
      error: (err) => {
        console.error('Failed to load metric instruments:', err);
        this.isLoadingInstruments.set(false);
      }
    });
  }

  loadSpans(): void {
    this.isLoadingSpans.set(true);
    this.obsService.getRecentSpans(this.spanLimit()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => {
        this.spans.set(data);
        this.isLoadingSpans.set(false);
      },
      error: (err) => {
        console.error('Failed to load spans:', err);
        this.isLoadingSpans.set(false);
      }
    });
  }

  toggleAutoRefresh(): void {
    const next = !this.isAutoRefresh();
    this.isAutoRefresh.set(next);

    if (next) {
      this.timerId = setInterval(() => {
        this.loadSummary();
        this.loadInstruments();
        this.loadSpans();
      }, 5000);
      this.toast.info('Auto-refresh activated (5s).');
    } else {
      if (this.timerId) {
        clearInterval(this.timerId);
        this.timerId = null;
      }
      this.toast.info('Auto-refresh paused.');
    }
  }

  togglePrometheusModal(): void {
    const next = !this.showPrometheusModal();
    this.showPrometheusModal.set(next);
    if (next && !this.rawPrometheusText()) {
      this.obsService.getPrometheusMetrics().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: (text) => this.rawPrometheusText.set(text),
        error: (err) => console.error('Failed to get Prometheus metrics:', err)
      });
    }
  }

  async copyRawPrometheus(): Promise<void> {
    if (!this.rawPrometheusText()) return;
    try {
      await navigator.clipboard.writeText(this.rawPrometheusText());
      this.copiedPrometheus.set(true);
      setTimeout(() => this.copiedPrometheus.set(false), 2000);
    } catch {}
  }

  triggerSyntheticTrace(): void {
    this.isGeneratingTrace.set(true);
    const cmd: SyntheticTraceCommand = {
      eventType: this.syntheticEventType || 'order.completed',
      includeFailure: this.syntheticIncludeFailure,
      delayMs: this.syntheticDelayMs
    };

    this.obsService.generateSyntheticTrace(cmd).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.syntheticResult.set(res);
        this.isGeneratingTrace.set(false);
        this.toast.success(`Synthetic trace ${res.traceId.slice(0, 8)}... generated!`);
        // Refresh spans and summary
        this.loadSummary();
        this.loadInstruments();
        this.loadSpans();
      },
      error: (err) => {
        console.error('Failed to generate synthetic trace:', err);
        this.isGeneratingTrace.set(false);
        this.toast.error('Failed to generate synthetic trace.');
      }
    });
  }

  toggleSpanDetails(span: CapturedSpan): void {
    if (this.selectedSpan()?.spanId === span.spanId) {
      this.selectedSpan.set(null);
    } else {
      this.selectedSpan.set(span);
    }
  }

  async copyText(text: string): Promise<void> {
    if (!text) return;
    try {
      await navigator.clipboard.writeText(text);
      this.toast.success('Copied to clipboard!');
    } catch {}
  }

  getInstrumentTypeBadgeClasses(type: string): string {
    if (type.includes('Counter')) return 'bg-brand-500/20 text-brand-300 border border-brand-500/30';
    if (type.includes('Histogram')) return 'bg-violet-500/20 text-violet-300 border border-violet-500/30';
    if (type.includes('Gauge')) return 'bg-amber-500/20 text-amber-300 border border-amber-500/30';
    return 'bg-surface-800 text-surface-300 border border-surface-700';
  }

  objectKeys(obj: Record<string, string> | undefined): string[] {
    return Object.keys(obj || {});
  }

  objectEntries(obj: Record<string, string> | undefined): Array<[string, string]> {
    return Object.entries(obj || {});
  }
}
