import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { SignalRService } from '../../core/signalr/services/signalr.service';
import { AuthService } from '../../core/auth/services/auth.service';
import { StatusBadgeComponent } from '../../shared/components/ui/status-badge.component';
import { SkeletonLoaderComponent } from '../../shared/components/ui/skeleton-loader.component';
import { DeliveryStats } from '../../shared/models/control-plane.models';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, StatusBadgeComponent, SkeletonLoaderComponent],
  template: `
    <div class="space-y-6">
      <!-- Header banner -->
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-xl font-bold tracking-tight text-white">Gateway Overview</h1>
          <p class="text-xs text-surface-400 mt-0.5">
            Real-time delivery telemetry and health metrics for {{ auth.tenantIdentifier() }}
          </p>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="refreshStats()" [disabled]="isLoading()"
                  class="px-3 py-1.5 bg-surface-900 border border-surface-700 hover:bg-surface-800 text-surface-300 hover:text-white rounded-lg text-xs font-medium transition-colors flex items-center gap-1.5">
            <svg class="w-3.5 h-3.5" [ngClass]="{'animate-spin': isLoading()}" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
            </svg>
            Refresh
          </button>
        </div>
      </div>

      <!-- KPI Metrics Cards Grid -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <!-- 1. Total Deliveries -->
        <div class="bg-surface-900/80 border border-surface-800 p-5 rounded-xl">
          <div class="text-xs font-medium text-surface-400 uppercase tracking-wider">Total Deliveries</div>
          <div class="mt-2 flex items-baseline justify-between">
            @if (isLoading()) {
              <app-skeleton-loader customClass="h-8 w-24"></app-skeleton-loader>
            } @else {
              <div class="text-2xl font-bold text-white font-mono">{{ stats()?.totalDeliveries ?? 0 }}</div>
            }
            <span class="text-xs text-brand-400 font-mono">Dispatched</span>
          </div>
        </div>

        <!-- 2. Success Rate -->
        <div class="bg-surface-900/80 border border-surface-800 p-5 rounded-xl">
          <div class="text-xs font-medium text-surface-400 uppercase tracking-wider">Success Rate</div>
          <div class="mt-2 flex items-baseline justify-between">
            @if (isLoading()) {
              <app-skeleton-loader customClass="h-8 w-20"></app-skeleton-loader>
            } @else {
              <div class="text-2xl font-bold font-mono"
                   [ngClass]="(stats()?.successRatePercentage ?? 0) >= 99 ? 'text-emerald-400' : 'text-amber-400'">
                {{ stats()?.successRatePercentage?.toFixed(1) ?? '100.0' }}%
              </div>
            }
            <span class="text-xs text-emerald-400/80 font-mono">Target: 99.9%</span>
          </div>
        </div>

        <!-- 3. Average Latency -->
        <div class="bg-surface-900/80 border border-surface-800 p-5 rounded-xl">
          <div class="text-xs font-medium text-surface-400 uppercase tracking-wider">Average Latency</div>
          <div class="mt-2 flex items-baseline justify-between">
            @if (isLoading()) {
              <app-skeleton-loader customClass="h-8 w-20"></app-skeleton-loader>
            } @else {
              <div class="text-2xl font-bold text-white font-mono">
                {{ stats()?.averageLatencyMs?.toFixed(0) ?? '0' }} <span class="text-xs font-normal text-surface-400">ms</span>
              </div>
            }
            <span class="text-xs text-surface-400 font-mono">p95 &lt; 200ms</span>
          </div>
        </div>

        <!-- 4. DLQ / Failed -->
        <div class="bg-surface-900/80 border border-surface-800 p-5 rounded-xl">
          <div class="text-xs font-medium text-surface-400 uppercase tracking-wider">Dead Letter Queue</div>
          <div class="mt-2 flex items-baseline justify-between">
            @if (isLoading()) {
              <app-skeleton-loader customClass="h-8 w-16"></app-skeleton-loader>
            } @else {
              <div class="text-2xl font-bold font-mono"
                   [ngClass]="(stats()?.deadLetteredDeliveries ?? 0) > 0 ? 'text-rose-400' : 'text-surface-300'">
                {{ stats()?.deadLetteredDeliveries ?? 0 }}
              </div>
            }
            <span class="text-xs font-mono"
                  [ngClass]="(stats()?.deadLetteredDeliveries ?? 0) > 0 ? 'text-rose-400' : 'text-emerald-400'">
              {{ (stats()?.deadLetteredDeliveries ?? 0) > 0 ? 'Action required' : 'Healthy' }}
            </span>
          </div>
        </div>
      </div>

      <!-- Live SignalR Delivery Stream Ticker -->
      <div class="bg-surface-900/80 border border-surface-800 rounded-xl overflow-hidden">
        <div class="p-4 border-b border-surface-800 flex items-center justify-between">
          <div class="flex items-center gap-2">
            <span class="relative flex h-2 w-2">
              <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-brand-400 opacity-75"></span>
              <span class="relative inline-flex rounded-full h-2 w-2 bg-brand-500"></span>
            </span>
            <h2 class="text-sm font-semibold text-white">Live Event Stream</h2>
            <span class="text-[10px] font-mono bg-surface-800 text-surface-400 px-2 py-0.5 rounded">
              {{ signalR.events().length }} events
            </span>
          </div>

          <div class="flex items-center gap-2">
            <button (click)="signalR.clearEvents()"
                    class="text-[11px] text-surface-400 hover:text-white px-2 py-1 rounded bg-surface-800 hover:bg-surface-700 transition-colors">
              Clear buffer
            </button>
          </div>
        </div>

        <div class="divide-y divide-surface-800/60 max-h-96 overflow-y-auto font-mono text-xs">
          @if (signalR.events().length === 0) {
            <div class="p-8 text-center text-surface-500 font-sans text-xs">
              <svg class="w-8 h-8 mx-auto text-surface-600 mb-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M13 10V3L4 14h7v7l9-11h-7z"/>
              </svg>
              Waiting for live webhook deliveries from SignalR hub...
            </div>
          } @else {
            @for (event of signalR.events(); track event.deliveryId + '-' + event.timestamp) {
              <div class="p-3.5 hover:bg-surface-800/40 flex items-center justify-between gap-4 transition-colors">
                <div class="flex items-center gap-3 min-w-0">
                  <app-status-badge [status]="event.status"></app-status-badge>
                  <span class="text-white font-medium truncate">{{ event.eventName }}</span>
                  <span class="text-surface-400 text-[11px] hidden sm:inline truncate max-w-xs">{{ event.deliveryId }}</span>
                </div>

                <div class="flex items-center gap-4 shrink-0 text-[11px] text-surface-400">
                  @if (event.attempt) {
                    <span class="text-surface-300 font-semibold">{{ event.attempt.httpStatusCode }}</span>
                    <span>{{ event.attempt.elapsedMs }}ms</span>
                  }
                  <span class="text-surface-500">{{ event.timestamp | date:'HH:mm:ss' }}</span>
                </div>
              </div>
            }
          }
        </div>
      </div>
    </div>
  `
})
export class DashboardComponent implements OnInit {
  readonly auth = inject(AuthService);
  readonly signalR = inject(SignalRService);
  private readonly http = inject(HttpClient);

  readonly stats = signal<DeliveryStats | null>(null);
  readonly isLoading = signal<boolean>(true);

  ngOnInit(): void {
    this.refreshStats();
  }

  refreshStats(): void {
    this.isLoading.set(true);
    this.http.get<DeliveryStats>(`${environment.apiBaseUrl}/deliveries/stats`).subscribe({
      next: (res) => {
        this.stats.set(res);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      }
    });
  }
}
