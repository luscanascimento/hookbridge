import { Component, OnInit, OnDestroy, computed, inject, signal, effect, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { StatusBadgeComponent } from '../../shared/components/ui/status-badge.component';
import { SkeletonLoaderComponent } from '../../shared/components/ui/skeleton-loader.component';
import { ButtonComponent } from '../../shared/components/ui/button.component';
import { DeliveryInspectorDrawerComponent } from './delivery-inspector-drawer.component';
import { BulkReplayModalComponent } from './bulk-replay-modal.component';
import { DeliveryService, DeliveryQueryParams } from '../../core/services/delivery.service';
import { EndpointService } from '../../core/services/endpoint.service';
import { SignalRService } from '../../core/signalr/services/signalr.service';
import { ToastService } from '../../shared/components/ui/toast/toast.service';
import { Delivery, Endpoint, PagedList } from '../../shared/models/control-plane.models';
import { RealtimeDeliveryEvent, DeliveryStatus } from '../../core/signalr/models/signalr.models';

export type DeliveryViewMode = 'live' | 'history';

@Component({
  selector: 'app-deliveries',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    StatusBadgeComponent,
    SkeletonLoaderComponent,
    ButtonComponent,
    DeliveryInspectorDrawerComponent,
    BulkReplayModalComponent
  ],
  template: `
    <div class="space-y-6">
      
      <!-- Page Header & Mode Switcher -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <div class="flex items-center gap-2.5">
            <h1 class="text-xl font-bold tracking-tight text-white">Live Event Inspector & Deliveries</h1>
            @if (mode() === 'live') {
              <span class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-mono font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 animate-pulse">
                <span class="w-1.5 h-1.5 rounded-full bg-emerald-400"></span>
                LIVE STREAM
              </span>
            }
          </div>
          <p class="text-xs text-surface-400 mt-0.5">
            Real-time webhook timeline, outbound HTTP dispatch inspection, cryptographic signatures, and delivery replays
          </p>
        </div>

        <!-- Top Right Actions & Mode Selector -->
        <div class="flex items-center gap-2.5">
          <!-- Mode Tabs Switcher -->
          <div class="p-1 bg-surface-900 border border-surface-800 rounded-xl flex items-center">
            <button
              (click)="setMode('live')"
              type="button"
              class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-all"
              [ngClass]="mode() === 'live' ? 'bg-surface-800 text-white shadow-xs font-semibold' : 'text-surface-400 hover:text-white'">
              <span class="w-2 h-2 rounded-full" [ngClass]="isStreamPaused() ? 'bg-amber-400' : 'bg-emerald-400 animate-ping'"></span>
              <span>Live Stream</span>
            </button>
            <button
              (click)="setMode('history')"
              type="button"
              class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-all"
              [ngClass]="mode() === 'history' ? 'bg-surface-800 text-white shadow-xs font-semibold' : 'text-surface-400 hover:text-white'">
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/>
              </svg>
              <span>Historical Log</span>
            </button>
          </div>

          <!-- Bulk Replay Action -->
          <button
            (click)="openBulkReplayModal()"
            type="button"
            class="px-3 py-2 bg-surface-900 border border-surface-700 hover:bg-surface-800 text-surface-300 hover:text-white rounded-lg text-xs font-medium transition-colors flex items-center gap-1.5">
            <svg class="w-3.5 h-3.5 text-brand-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
            </svg>
            <span>Bulk Replay</span>
          </button>

          <!-- Refresh Action -->
          <button
            (click)="refreshCurrentView()"
            [disabled]="isLoading()"
            class="p-2 bg-surface-900 border border-surface-700 hover:bg-surface-800 text-surface-300 hover:text-white rounded-lg text-xs transition-colors"
            title="Refresh">
            <svg class="w-4 h-4" [ngClass]="{'animate-spin': isLoading()}" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
            </svg>
          </button>
        </div>
      </div>

      <!-- ========================================================================= -->
      <!-- LIVE STREAM MODE -->
      <!-- ========================================================================= -->
      @if (mode() === 'live') {
        <div class="space-y-4">
          
          <!-- Live Stream Control Bar -->
          <div class="p-4 bg-surface-900/90 border border-surface-800 rounded-xl flex flex-wrap items-center justify-between gap-4">
            
            <!-- Left Controls: Stream State & Stream Stats -->
            <div class="flex items-center gap-3">
              <!-- Pause / Resume Toggle Button -->
              <button
                (click)="toggleStreamPause()"
                type="button"
                class="px-3 py-1.5 rounded-lg text-xs font-medium transition-all flex items-center gap-2"
                [ngClass]="isStreamPaused() ? 'bg-amber-500/20 border border-amber-500/40 text-amber-300 hover:bg-amber-500/30' : 'bg-surface-800 border border-surface-700 text-surface-300 hover:text-white hover:bg-surface-700'">
                @if (isStreamPaused()) {
                  <svg class="w-3.5 h-3.5 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z"/>
                  </svg>
                  <span>Resume Stream</span>
                  @if (pausedQueue().length > 0) {
                    <span class="px-1.5 py-0.2 bg-amber-400 text-surface-950 font-bold font-mono text-[10px] rounded-full">
                      +{{ pausedQueue().length }}
                    </span>
                  }
                } @else {
                  <svg class="w-3.5 h-3.5 text-surface-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 9v6m4-6v6m7-3a9 9 0 11-18 0 9 9 0 0118 0z"/>
                  </svg>
                  <span>Pause Stream</span>
                }
              </button>

              <!-- Auto-Scroll Checkbox -->
              <label class="flex items-center gap-2 text-xs text-surface-400 cursor-pointer select-none">
                <input
                  type="checkbox"
                  [(ngModel)]="autoScroll"
                  class="rounded bg-surface-950 border-surface-700 text-brand-500 focus:ring-brand-500 focus:ring-offset-surface-900" />
                <span>Auto-scroll to latest</span>
              </label>

              <!-- Clear Stream Buffer -->
              <button
                (click)="clearStreamBuffer()"
                type="button"
                class="text-xs text-surface-500 hover:text-surface-300 transition-colors">
                Clear Buffer
              </button>
            </div>

            <!-- Right Controls: Fast Live Filters -->
            <div class="flex flex-wrap items-center gap-2.5 text-xs">
              <!-- Event Type Filter -->
              <input
                type="text"
                [(ngModel)]="liveEventTypeFilter"
                placeholder="Filter event type (e.g. order.*)..."
                class="px-2.5 py-1.5 bg-surface-950 border border-surface-800 rounded-lg text-white font-mono text-xs focus:border-brand-500 focus:outline-none w-52 placeholder:text-surface-600" />

              <!-- Status Filter Dropdown -->
              <select
                [(ngModel)]="liveStatusFilter"
                class="px-2.5 py-1.5 bg-surface-950 border border-surface-800 rounded-lg text-surface-300 text-xs focus:border-brand-500 focus:outline-none">
                <option value="">All Statuses</option>
                <option value="Success">Success</option>
                <option value="Failed">Failed</option>
                <option value="DeadLettered">Dead-Lettered</option>
                <option value="Dispatched">Dispatched</option>
              </select>

              <!-- Endpoint Filter Dropdown -->
              <select
                [(ngModel)]="liveEndpointFilter"
                class="px-2.5 py-1.5 bg-surface-950 border border-surface-800 rounded-lg text-surface-300 text-xs focus:border-brand-500 focus:outline-none max-w-[160px] truncate">
                <option value="">All Endpoints</option>
                @for (ep of endpoints(); track ep.id) {
                  <option [value]="ep.id">{{ ep.targetUrl }}</option>
                }
              </select>
            </div>
          </div>

          <!-- Paused Queue Banner -->
          @if (isStreamPaused() && pausedQueue().length > 0) {
            <div
              (click)="flushPausedQueue()"
              class="p-3 bg-amber-950/40 border border-amber-800/60 rounded-xl text-xs text-amber-300 flex items-center justify-between cursor-pointer hover:bg-amber-950/60 transition-colors animate-fadeIn">
              <div class="flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-amber-400"></span>
                <span>Stream is paused. <strong>{{ pausedQueue().length }} new events</strong> waiting to be displayed.</span>
              </div>
              <span class="font-medium underline">Click to flush & view now &rarr;</span>
            </div>
          }

          <!-- Live Events Feed Container -->
          <div class="bg-surface-900/80 border border-surface-800 rounded-xl overflow-hidden shadow-xl">
            @if (filteredLiveEvents().length === 0) {
              <div class="p-16 text-center">
                <div class="w-12 h-12 rounded-full bg-surface-800 flex items-center justify-center mx-auto mb-3 text-surface-500">
                  <svg class="w-6 h-6 animate-pulse" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M13 10V3L4 14h7v7l9-11h-7z"/>
                  </svg>
                </div>
                <div class="text-sm font-semibold text-surface-200">Listening for live webhook events...</div>
                <p class="text-xs text-surface-500 mt-1 max-w-sm mx-auto">
                  Publish events via API or EventFlow to see live outbound dispatches, attempts, and status transitions stream in real-time.
                </p>
              </div>
            } @else {
              <div class="divide-y divide-surface-800/60 font-sans text-xs">
                @for (evt of filteredLiveEvents(); track evt.deliveryId + '-' + evt.timestamp) {
                  <div
                    (click)="openInspector(evt.deliveryId, evt)"
                    class="p-4 hover:bg-surface-800/50 transition-colors cursor-pointer flex flex-col sm:flex-row sm:items-center justify-between gap-3 group">
                    
                    <!-- Left: Event Name, Status & Endpoint -->
                    <div class="flex items-center gap-3 min-w-0">
                      <app-status-badge [status]="evt.status"></app-status-badge>

                      <div class="min-w-0">
                        <div class="flex items-center gap-2">
                          <span class="text-sm font-bold text-white font-mono group-hover:text-brand-300 transition-colors">
                            {{ evt.eventName }}
                          </span>
                          @if (evt.originalDeliveryId) {
                            <span class="px-1.5 py-0.2 rounded text-[10px] font-mono bg-brand-500/20 text-brand-300 border border-brand-500/30">
                              REPLAY
                            </span>
                          }
                        </div>

                        <div class="flex items-center gap-3 mt-1 text-[11px] text-surface-400 font-mono">
                          <span class="text-surface-300 truncate max-w-xs" [title]="resolveEndpointUrl(evt)">
                            {{ resolveEndpointUrl(evt) }}
                          </span>
                          <span class="text-surface-600">•</span>
                          <span>ID: {{ evt.deliveryId.slice(0, 8) }}...</span>
                          <span class="text-surface-600">•</span>
                          <span>Corr: {{ evt.correlationId.slice(0, 8) }}...</span>
                        </div>
                      </div>
                    </div>

                    <!-- Right: Attempt & Status info, Latency, Timestamp -->
                    <div class="flex items-center gap-3.5 shrink-0 font-mono text-xs">
                      @if (evt.attempt) {
                        <!-- HTTP Status Code -->
                        <span
                          class="px-2 py-0.5 rounded text-[11px] font-semibold"
                          [ngClass]="getHttpStatusClasses(evt.attempt.httpStatusCode)">
                          {{ evt.attempt.httpStatusCode ? 'HTTP ' + evt.attempt.httpStatusCode : 'Pending' }}
                        </span>

                        <!-- Latency -->
                        <span class="px-2 py-0.5 rounded bg-surface-800 text-surface-300 text-[11px]">
                          {{ evt.attempt.elapsedMs }}ms
                        </span>
                      } @else {
                        <span class="text-surface-500 text-[11px]">Attempt #{{ evt.attemptCount || 1 }}</span>
                      }

                      <!-- Timestamp -->
                      <span class="text-surface-500 text-[11px]">
                        {{ evt.timestamp | date:'HH:mm:ss.SSS' }}
                      </span>

                      <!-- Arrow Indicator -->
                      <svg class="w-4 h-4 text-surface-600 group-hover:text-white group-hover:translate-x-0.5 transition-all" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/>
                      </svg>
                    </div>
                  </div>
                }
              </div>
            }
          </div>
        </div>
      }

      <!-- ========================================================================= -->
      <!-- HISTORICAL LOG & SEARCH MODE -->
      <!-- ========================================================================= -->
      @if (mode() === 'history') {
        <div class="space-y-4">
          
          <!-- Comprehensive Filters Bar -->
          <div class="p-4 bg-surface-900/90 border border-surface-800 rounded-xl space-y-3">
            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3 text-xs">
              
              <!-- Correlation ID / ID Search -->
              <div>
                <label class="block text-[11px] font-semibold text-surface-400 uppercase tracking-wider mb-1">
                  Search ID / Correlation
                </label>
                <input
                  type="text"
                  [(ngModel)]="historyQueryCorrelationId"
                  (keyup.enter)="loadHistoryDeliveries()"
                  placeholder="Correlation ID..."
                  class="w-full px-2.5 py-1.5 bg-surface-950 border border-surface-800 rounded-lg text-white font-mono text-xs focus:border-brand-500 focus:outline-none" />
              </div>

              <!-- Event Type Filter -->
              <div>
                <label class="block text-[11px] font-semibold text-surface-400 uppercase tracking-wider mb-1">
                  Event Type
                </label>
                <input
                  type="text"
                  [(ngModel)]="historyQueryEventType"
                  (keyup.enter)="loadHistoryDeliveries()"
                  placeholder="e.g. order.created"
                  class="w-full px-2.5 py-1.5 bg-surface-950 border border-surface-800 rounded-lg text-white font-mono text-xs focus:border-brand-500 focus:outline-none" />
              </div>

              <!-- Endpoint Filter -->
              <div>
                <label class="block text-[11px] font-semibold text-surface-400 uppercase tracking-wider mb-1">
                  Endpoint
                </label>
                <select
                  [(ngModel)]="historyQueryEndpointId"
                  (change)="loadHistoryDeliveries()"
                  class="w-full px-2.5 py-1.5 bg-surface-950 border border-surface-800 rounded-lg text-surface-300 text-xs focus:border-brand-500 focus:outline-none">
                  <option [ngValue]="null">All Endpoints</option>
                  @for (ep of endpoints(); track ep.id) {
                    <option [ngValue]="ep.id">{{ ep.targetUrl }}</option>
                  }
                </select>
              </div>

              <!-- Status Filter -->
              <div>
                <label class="block text-[11px] font-semibold text-surface-400 uppercase tracking-wider mb-1">
                  Status
                </label>
                <select
                  [(ngModel)]="historyQueryStatus"
                  (change)="loadHistoryDeliveries()"
                  class="w-full px-2.5 py-1.5 bg-surface-950 border border-surface-800 rounded-lg text-surface-300 text-xs focus:border-brand-500 focus:outline-none">
                  <option [ngValue]="null">All Statuses</option>
                  <option value="Pending">Pending</option>
                  <option value="Dispatched">Dispatched</option>
                  <option value="Success">Success</option>
                  <option value="Failed">Failed</option>
                  <option value="DeadLettered">DeadLettered</option>
                </select>
              </div>

              <!-- Time Range Filter -->
              <div>
                <label class="block text-[11px] font-semibold text-surface-400 uppercase tracking-wider mb-1">
                  Time Range
                </label>
                <select
                  [(ngModel)]="historyQueryTimeRange"
                  (change)="onTimeRangeChange()"
                  class="w-full px-2.5 py-1.5 bg-surface-950 border border-surface-800 rounded-lg text-surface-300 text-xs focus:border-brand-500 focus:outline-none">
                  <option value="all">All Time</option>
                  <option value="15m">Past 15 minutes</option>
                  <option value="1h">Past 1 hour</option>
                  <option value="24h">Past 24 hours</option>
                  <option value="7d">Past 7 days</option>
                </select>
              </div>
            </div>

            <!-- Filter Action Buttons -->
            <div class="flex items-center justify-between pt-1 border-t border-surface-800/60">
              <div class="text-[11px] text-surface-500 font-mono">
                Showing page {{ historyPage() }} of {{ historyTotalPages() }} ({{ historyTotalCount() }} total deliveries)
              </div>
              
              <div class="flex items-center gap-2">
                <button
                  (click)="resetHistoryFilters()"
                  type="button"
                  class="px-2.5 py-1 text-xs text-surface-400 hover:text-white transition-colors">
                  Reset Filters
                </button>
                <app-button
                  variant="secondary"
                  size="sm"
                  (clicked)="loadHistoryDeliveries()">
                  Apply Filters
                </app-button>
              </div>
            </div>
          </div>

          <!-- History Table Container -->
          <div class="bg-surface-900/80 border border-surface-800 rounded-xl overflow-hidden shadow-xl">
            @if (isLoading()) {
              <div class="p-6 space-y-4">
                <app-skeleton-loader customClass="h-10 w-full"></app-skeleton-loader>
                <app-skeleton-loader customClass="h-10 w-full"></app-skeleton-loader>
                <app-skeleton-loader customClass="h-10 w-full"></app-skeleton-loader>
                <app-skeleton-loader customClass="h-10 w-full"></app-skeleton-loader>
              </div>
            } @else if (historyDeliveries().length === 0) {
              <div class="p-12 text-center text-surface-500">
                <svg class="w-10 h-10 mx-auto text-surface-600 mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"/>
                </svg>
                <div class="text-sm font-medium text-surface-300">No deliveries found</div>
                <p class="text-xs text-surface-500 mt-1">Try adjusting your filters or publish new events to record webhook dispatches.</p>
              </div>
            } @else {
              <div class="overflow-x-auto">
                <table class="w-full text-left border-collapse text-xs">
                  <thead>
                    <tr class="border-b border-surface-800 bg-surface-950/60 text-surface-400 uppercase tracking-wider font-semibold">
                      <th class="p-3.5 pl-5">Status</th>
                      <th class="p-3.5">Event Type</th>
                      <th class="p-3.5">Target Endpoint</th>
                      <th class="p-3.5">Attempts</th>
                      <th class="p-3.5">Correlation ID</th>
                      <th class="p-3.5">Scheduled At</th>
                      <th class="p-3.5 pr-5 text-right">Action</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-surface-800/60 font-mono">
                    @for (d of historyDeliveries(); track d.id) {
                      <tr
                        (click)="openInspector(d.id)"
                        class="hover:bg-surface-800/50 transition-colors cursor-pointer group">
                        
                        <td class="p-3.5 pl-5 font-sans">
                          <app-status-badge [status]="d.status"></app-status-badge>
                        </td>
                        
                        <td class="p-3.5 text-white font-medium truncate max-w-xs group-hover:text-brand-300 transition-colors">
                          {{ d.eventType }}
                          @if (d.originalDeliveryId) {
                            <span class="ml-1 text-[10px] text-brand-400 font-normal">[Replay]</span>
                          }
                        </td>
                        
                        <td class="p-3.5 text-surface-300 truncate max-w-[180px]" [title]="d.endpointUrl || d.endpointId">
                          {{ d.endpointUrl || d.endpointId }}
                        </td>

                        <td class="p-3.5 text-surface-300 font-sans">
                          {{ d.attemptCount }} attempt{{ d.attemptCount === 1 ? '' : 's' }}
                        </td>

                        <td class="p-3.5 text-surface-400 text-[11px] truncate max-w-[120px]" [title]="d.correlationId">
                          {{ d.correlationId }}
                        </td>

                        <td class="p-3.5 text-surface-500 font-sans">
                          {{ d.scheduledAt | date:'HH:mm:ss MMM d' }}
                        </td>

                        <td class="p-3.5 pr-5 text-right font-sans" (click)="$event.stopPropagation()">
                          <button
                            (click)="openInspector(d.id)"
                            class="px-2.5 py-1 bg-surface-800 hover:bg-surface-700 text-surface-200 text-xs rounded-lg transition-colors inline-flex items-center gap-1">
                            <span>Inspect</span>
                            <svg class="w-3 h-3 text-surface-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/>
                            </svg>
                          </button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>

              <!-- Pagination Footer -->
              <div class="p-4 border-t border-surface-800 bg-surface-950/40 flex items-center justify-between text-xs">
                <div class="flex items-center gap-2">
                  <span class="text-surface-400">Items per page:</span>
                  <select
                    [(ngModel)]="historyPageSize"
                    (change)="onPageSizeChange()"
                    class="bg-surface-900 border border-surface-800 rounded p-1 text-white font-mono text-xs focus:border-brand-500 focus:outline-none">
                    <option [ngValue]="20">20</option>
                    <option [ngValue]="50">50</option>
                    <option [ngValue]="100">100</option>
                  </select>
                </div>

                <div class="flex items-center gap-2">
                  <button
                    [disabled]="historyPage() <= 1"
                    (click)="goToPage(historyPage() - 1)"
                    class="px-3 py-1 bg-surface-900 border border-surface-800 rounded text-surface-300 hover:text-white disabled:opacity-40 disabled:cursor-not-allowed transition-colors">
                    Previous
                  </button>
                  <span class="text-surface-400 font-mono px-2">Page {{ historyPage() }} of {{ historyTotalPages() }}</span>
                  <button
                    [disabled]="historyPage() >= historyTotalPages()"
                    (click)="goToPage(historyPage() + 1)"
                    class="px-3 py-1 bg-surface-900 border border-surface-800 rounded text-surface-300 hover:text-white disabled:opacity-40 disabled:cursor-not-allowed transition-colors">
                    Next
                  </button>
                </div>
              </div>
            }
          </div>
        </div>
      }

      <!-- Slide-Over Inspector Drawer -->
      <app-delivery-inspector-drawer
        [isOpen]="isDrawerOpen()"
        [deliveryId]="selectedDeliveryId()"
        [realtimeEvent]="selectedRealtimeEvent()"
        [endpoints]="endpoints()"
        (closed)="closeInspector()"
        (replayed)="onDeliveryReplayed()">
      </app-delivery-inspector-drawer>

      <!-- Bulk Replay Modal -->
      <app-bulk-replay-modal
        [isOpen]="isBulkReplayModalOpen()"
        [endpoints]="endpoints()"
        [initialStatus]="historyQueryStatus()"
        [initialEndpointId]="historyQueryEndpointId()"
        [initialEventType]="historyQueryEventType()"
        (closed)="closeBulkReplayModal()"
        (replayed)="onBulkReplayed()">
      </app-bulk-replay-modal>

    </div>
  `
})
export class DeliveriesComponent implements OnInit, OnDestroy {
  private readonly deliveryService = inject(DeliveryService);
  private readonly endpointService = inject(EndpointService);
  readonly signalr = inject(SignalRService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  // View mode
  readonly mode = signal<DeliveryViewMode>('live');
  readonly isLoading = signal<boolean>(false);

  // Endpoints Cache
  readonly endpoints = signal<Endpoint[]>([]);

  // Live Stream State
  readonly isStreamPaused = signal<boolean>(false);
  readonly liveEvents = signal<RealtimeDeliveryEvent[]>([]);
  readonly pausedQueue = signal<RealtimeDeliveryEvent[]>([]);
  readonly autoScroll = signal<boolean>(true);
  readonly liveEventTypeFilter = signal<string>('');
  readonly liveStatusFilter = signal<string>('');
  readonly liveEndpointFilter = signal<string>('');

  // Historical Query State
  readonly historyDeliveries = signal<Delivery[]>([]);
  readonly historyPage = signal<number>(1);
  readonly historyPageSize = signal<number>(20);
  readonly historyTotalCount = signal<number>(0);
  readonly historyTotalPages = signal<number>(1);
  readonly historyQueryCorrelationId = signal<string>('');
  readonly historyQueryEventType = signal<string>('');
  readonly historyQueryEndpointId = signal<string | null>(null);
  readonly historyQueryStatus = signal<string | null>(null);
  readonly historyQueryTimeRange = signal<string>('all');
  readonly historyFromDate = signal<string | null>(null);

  // Drawer / Inspector State
  readonly isDrawerOpen = signal<boolean>(false);
  readonly selectedDeliveryId = signal<string | null>(null);
  readonly selectedRealtimeEvent = signal<RealtimeDeliveryEvent | null>(null);

  // Bulk Replay Modal State
  readonly isBulkReplayModalOpen = signal<boolean>(false);

  constructor() {
    // Listen to real-time events from SignalR
    effect(() => {
      const latest = this.signalr.latestEvent();
      if (latest) {
        this.handleIncomingRealtimeEvent(latest);
      }
    });

    // Handle query params on initial navigation
    this.route.queryParams.subscribe(params => {
      if (params['mode'] === 'history' || params['mode'] === 'live') {
        this.mode.set(params['mode'] as DeliveryViewMode);
      }
      if (params['deliveryId']) {
        this.openInspector(params['deliveryId']);
      }
    });
  }

  ngOnInit(): void {
    this.loadEndpoints();
    this.signalr.startConnection();
    if (this.mode() === 'history') {
      this.loadHistoryDeliveries();
    }
  }

  ngOnDestroy(): void {
    // Clean up if needed
  }

  readonly filteredLiveEvents = computed(() => {
    let list = this.liveEvents();

    const typeFilter = this.liveEventTypeFilter().trim().toLowerCase();
    if (typeFilter) {
      if (typeFilter.endsWith('*')) {
        const prefix = typeFilter.slice(0, -1);
        list = list.filter(e => e.eventName.toLowerCase().startsWith(prefix));
      } else {
        list = list.filter(e => e.eventName.toLowerCase().includes(typeFilter));
      }
    }

    const statusFilter = this.liveStatusFilter();
    if (statusFilter) {
      list = list.filter(e => e.status === statusFilter);
    }

    const epFilter = this.liveEndpointFilter();
    if (epFilter) {
      list = list.filter(e => e.endpointId === epFilter);
    }

    return list;
  });

  setMode(m: DeliveryViewMode): void {
    this.mode.set(m);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { mode: m },
      queryParamsHandling: 'merge'
    });

    if (m === 'history' && this.historyDeliveries().length === 0) {
      this.loadHistoryDeliveries();
    }
  }

  loadEndpoints(): void {
    this.endpointService.getEndpoints().subscribe({
      next: (data) => this.endpoints.set(data),
      error: () => {}
    });
  }

  handleIncomingRealtimeEvent(evt: RealtimeDeliveryEvent): void {
    if (this.isStreamPaused()) {
      this.pausedQueue.update(q => [evt, ...q]);
    } else {
      this.liveEvents.update(events => {
        const updated = [evt, ...events];
        return updated.slice(0, 150);
      });
    }
  }

  toggleStreamPause(): void {
    const paused = this.isStreamPaused();
    if (paused) {
      this.flushPausedQueue();
    }
    this.isStreamPaused.set(!paused);
  }

  flushPausedQueue(): void {
    const queued = this.pausedQueue();
    if (queued.length > 0) {
      this.liveEvents.update(events => {
        const updated = [...queued, ...events];
        return updated.slice(0, 150);
      });
      this.pausedQueue.set([]);
    }
    this.isStreamPaused.set(false);
  }

  clearStreamBuffer(): void {
    this.liveEvents.set([]);
    this.pausedQueue.set([]);
    this.signalr.clearEvents();
  }

  resolveEndpointUrl(evt: RealtimeDeliveryEvent): string {
    if (evt.endpointUrl) return evt.endpointUrl;
    const ep = this.endpoints().find(e => e.id === evt.endpointId);
    return ep ? ep.targetUrl : evt.endpointId;
  }

  getHttpStatusClasses(code?: number | null): string {
    if (!code) return 'bg-surface-800 text-surface-400';
    if (code >= 200 && code < 300) return 'bg-emerald-500/20 text-emerald-300';
    if (code >= 400 && code < 500) return 'bg-amber-500/20 text-amber-300';
    return 'bg-rose-500/20 text-rose-300';
  }

  // =========================================================================
  // Historical Log Loading
  // =========================================================================

  loadHistoryDeliveries(): void {
    this.isLoading.set(true);

    const query: DeliveryQueryParams = {
      correlationId: this.historyQueryCorrelationId().trim() || null,
      eventType: this.historyQueryEventType().trim() || null,
      endpointId: this.historyQueryEndpointId(),
      status: (this.historyQueryStatus() as DeliveryStatus) || null,
      fromDate: this.historyFromDate(),
      page: this.historyPage(),
      pageSize: this.historyPageSize()
    };

    this.deliveryService.getDeliveries(query).subscribe({
      next: (res: PagedList<Delivery>) => {
        this.historyDeliveries.set(res.items || []);
        this.historyTotalCount.set(res.totalCount || 0);
        this.historyTotalPages.set(res.totalPages || 1);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load deliveries:', err);
        this.isLoading.set(false);
        this.toast.error('Failed to load delivery history records.');
      }
    });
  }

  onTimeRangeChange(): void {
    const range = this.historyQueryTimeRange();
    const now = new Date();

    switch (range) {
      case '15m':
        this.historyFromDate.set(new Date(now.getTime() - 15 * 60 * 1000).toISOString());
        break;
      case '1h':
        this.historyFromDate.set(new Date(now.getTime() - 60 * 60 * 1000).toISOString());
        break;
      case '24h':
        this.historyFromDate.set(new Date(now.getTime() - 24 * 60 * 60 * 1000).toISOString());
        break;
      case '7d':
        this.historyFromDate.set(new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000).toISOString());
        break;
      default:
        this.historyFromDate.set(null);
        break;
    }

    this.historyPage.set(1);
    this.loadHistoryDeliveries();
  }

  resetHistoryFilters(): void {
    this.historyQueryCorrelationId.set('');
    this.historyQueryEventType.set('');
    this.historyQueryEndpointId.set(null);
    this.historyQueryStatus.set(null);
    this.historyQueryTimeRange.set('all');
    this.historyFromDate.set(null);
    this.historyPage.set(1);
    this.loadHistoryDeliveries();
  }

  goToPage(page: number): void {
    this.historyPage.set(page);
    this.loadHistoryDeliveries();
  }

  onPageSizeChange(): void {
    this.historyPage.set(1);
    this.loadHistoryDeliveries();
  }

  refreshCurrentView(): void {
    if (this.mode() === 'live') {
      this.loadEndpoints();
      this.toast.info('Live stream buffers updated.', 'Refreshed');
    } else {
      this.loadHistoryDeliveries();
      this.toast.info('Historical deliveries updated.', 'Refreshed');
    }
  }

  // =========================================================================
  // Inspector & Modal Handling
  // =========================================================================

  openInspector(deliveryId: string, rtEvent?: RealtimeDeliveryEvent): void {
    this.selectedDeliveryId.set(deliveryId);
    this.selectedRealtimeEvent.set(rtEvent || null);
    this.isDrawerOpen.set(true);
  }

  closeInspector(): void {
    this.isDrawerOpen.set(false);
    this.selectedDeliveryId.set(null);
    this.selectedRealtimeEvent.set(null);
  }

  onDeliveryReplayed(): void {
    if (this.mode() === 'history') {
      this.loadHistoryDeliveries();
    }
  }

  openBulkReplayModal(): void {
    this.isBulkReplayModalOpen.set(true);
  }

  closeBulkReplayModal(): void {
    this.isBulkReplayModalOpen.set(false);
  }

  onBulkReplayed(): void {
    if (this.mode() === 'history') {
      this.loadHistoryDeliveries();
    }
  }
}
