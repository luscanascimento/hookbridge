import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { StatusBadgeComponent } from '../../shared/components/ui/status-badge.component';
import { SkeletonLoaderComponent } from '../../shared/components/ui/skeleton-loader.component';
import { Endpoint } from '../../shared/models/control-plane.models';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-endpoints',
  standalone: true,
  imports: [CommonModule, StatusBadgeComponent, SkeletonLoaderComponent],
  template: `
    <div class="space-y-6">
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-xl font-bold tracking-tight text-white">Webhook Endpoints</h1>
          <p class="text-xs text-surface-400 mt-0.5">
            Registered webhook destinations, signing secrets, and event subscriptions
          </p>
        </div>
      </div>

      <div class="bg-surface-900/80 border border-surface-800 rounded-xl overflow-hidden">
        @if (isLoading()) {
          <div class="p-6 space-y-4">
            <app-skeleton-loader customClass="h-10 w-full"></app-skeleton-loader>
            <app-skeleton-loader customClass="h-10 w-full"></app-skeleton-loader>
            <app-skeleton-loader customClass="h-10 w-full"></app-skeleton-loader>
          </div>
        } @else if (endpoints().length === 0) {
          <div class="p-12 text-center text-surface-500">
            <svg class="w-10 h-10 mx-auto text-surface-600 mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M13 10V3L4 14h7v7l9-11h-7z"/>
            </svg>
            <div class="text-sm font-medium text-surface-300">No endpoints registered</div>
            <p class="text-xs text-surface-500 mt-1">Create an endpoint to start routing webhook events.</p>
          </div>
        } @else {
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-xs">
              <thead>
                <tr class="border-b border-surface-800 bg-surface-950/50 text-surface-400 uppercase tracking-wider font-semibold">
                  <th class="p-3.5 pl-5">Status</th>
                  <th class="p-3.5">Target URL</th>
                  <th class="p-3.5">Rate Limit</th>
                  <th class="p-3.5">Timeout</th>
                  <th class="p-3.5 pr-5">Created</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-surface-800/60 font-mono">
                @for (ep of endpoints(); track ep.id) {
                  <tr class="hover:bg-surface-800/40 transition-colors">
                    <td class="p-3.5 pl-5 font-sans">
                      <app-status-badge [status]="ep.status"></app-status-badge>
                    </td>
                    <td class="p-3.5 text-white font-medium truncate max-w-xs">{{ ep.targetUrl }}</td>
                    <td class="p-3.5 text-surface-300">{{ ep.rateLimitPerMinute }} req/min</td>
                    <td class="p-3.5 text-surface-300">{{ ep.timeoutSeconds }}s</td>
                    <td class="p-3.5 pr-5 text-surface-500 font-sans">{{ ep.createdAt | date:'MMM d, y' }}</td>
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
export class EndpointsComponent implements OnInit {
  private readonly http = inject(HttpClient);

  readonly endpoints = signal<Endpoint[]>([]);
  readonly isLoading = signal(true);

  ngOnInit(): void {
    this.loadEndpoints();
  }

  loadEndpoints(): void {
    this.isLoading.set(true);
    this.http.get<Endpoint[]>(`${environment.apiBaseUrl}/endpoints`).subscribe({
      next: (res) => {
        this.endpoints.set(res || []);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      }
    });
  }
}
