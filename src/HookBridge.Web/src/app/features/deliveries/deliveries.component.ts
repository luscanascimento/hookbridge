import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { StatusBadgeComponent } from '../../shared/components/ui/status-badge.component';
import { SkeletonLoaderComponent } from '../../shared/components/ui/skeleton-loader.component';
import { Delivery } from '../../shared/models/control-plane.models';
import { environment } from '../../../environments/environment';

interface PagedList<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

@Component({
  selector: 'app-deliveries',
  standalone: true,
  imports: [CommonModule, StatusBadgeComponent, SkeletonLoaderComponent],
  template: `
    <div class="space-y-6">
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-xl font-bold tracking-tight text-white">Webhook Deliveries</h1>
          <p class="text-xs text-surface-400 mt-0.5">
            Audit history, outbound HTTP dispatch attempts, and delivery replay timeline
          </p>
        </div>

        <button (click)="loadDeliveries()" [disabled]="isLoading()"
                class="px-3 py-1.5 bg-surface-900 border border-surface-700 hover:bg-surface-800 text-surface-300 hover:text-white rounded-lg text-xs font-medium transition-colors flex items-center gap-1.5">
          <svg class="w-3.5 h-3.5" [ngClass]="{'animate-spin': isLoading()}" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
          </svg>
          Refresh
        </button>
      </div>

      <div class="bg-surface-900/80 border border-surface-800 rounded-xl overflow-hidden">
        @if (isLoading()) {
          <div class="p-6 space-y-4">
            <app-skeleton-loader customClass="h-10 w-full"></app-skeleton-loader>
            <app-skeleton-loader customClass="h-10 w-full"></app-skeleton-loader>
            <app-skeleton-loader customClass="h-10 w-full"></app-skeleton-loader>
          </div>
        } @else if (deliveries().length === 0) {
          <div class="p-12 text-center text-surface-500">
            <svg class="w-10 h-10 mx-auto text-surface-600 mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"/>
            </svg>
            <div class="text-sm font-medium text-surface-300">No deliveries recorded</div>
            <p class="text-xs text-surface-500 mt-1">Publish an event to trigger webhook deliveries.</p>
          </div>
        } @else {
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-xs">
              <thead>
                <tr class="border-b border-surface-800 bg-surface-950/50 text-surface-400 uppercase tracking-wider font-semibold">
                  <th class="p-3.5 pl-5">Status</th>
                  <th class="p-3.5">Event Type</th>
                  <th class="p-3.5">Delivery ID</th>
                  <th class="p-3.5">Attempts</th>
                  <th class="p-3.5">Correlation ID</th>
                  <th class="p-3.5 pr-5">Scheduled At</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-surface-800/60 font-mono">
                @for (d of deliveries(); track d.id) {
                  <tr class="hover:bg-surface-800/40 transition-colors">
                    <td class="p-3.5 pl-5 font-sans">
                      <app-status-badge [status]="d.status"></app-status-badge>
                    </td>
                    <td class="p-3.5 text-white font-medium truncate max-w-xs">{{ d.eventType }}</td>
                    <td class="p-3.5 text-surface-400 text-[11px] truncate max-w-[120px]">{{ d.id }}</td>
                    <td class="p-3.5 text-surface-300">{{ d.attemptCount }}</td>
                    <td class="p-3.5 text-surface-400 text-[11px] truncate max-w-[120px]">{{ d.correlationId }}</td>
                    <td class="p-3.5 pr-5 text-surface-500 font-sans">{{ d.scheduledAt | date:'HH:mm:ss MMM d' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `
})
export class DeliveriesComponent implements OnInit {
  private readonly http = inject(HttpClient);

  readonly deliveries = signal<Delivery[]>([]);
  readonly isLoading = signal(true);

  ngOnInit(): void {
    this.loadDeliveries();
  }

  loadDeliveries(): void {
    this.isLoading.set(true);
    this.http.get<PagedList<Delivery>>(`${environment.apiBaseUrl}/deliveries?pageSize=50`).subscribe({
      next: (res) => {
        this.deliveries.set(res.items || []);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      }
    });
  }
}
