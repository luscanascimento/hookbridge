import { Component, OnInit, OnDestroy, computed, inject, signal, effect, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SandboxService } from '../../core/services/sandbox.service';
import { SignalRService } from '../../core/signalr/services/signalr.service';
import { ToastService } from '../../shared/components/ui/toast/toast.service';
import {
  WebhookSandbox,
  SandboxRequestSummary,
  SandboxRequestDetail,
  CreateSandboxRequest,
  UpdateSandboxConfigRequest
} from '../../core/models/sandbox.models';
import { CodeViewerComponent } from '../../shared/components/ui/code-viewer.component';
import { SkeletonLoaderComponent } from '../../shared/components/ui/skeleton-loader.component';

@Component({
  selector: 'app-sandbox',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    CodeViewerComponent,
    SkeletonLoaderComponent
  ],
  template: `
    <div class="space-y-6 max-w-7xl mx-auto pb-16">
      <!-- Header Banner -->
      <div class="bg-gradient-to-r from-surface-900 via-surface-900/95 to-indigo-950/40 p-6 rounded-2xl border border-surface-800 shadow-xl flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div class="flex items-center gap-2">
            <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">
              Webhook Sandbox
            </span>
            <span class="flex items-center gap-1.5 text-xs text-surface-400 font-mono">
              <span class="w-2 h-2 rounded-full" [ngClass]="signalR.status() === 'connected' ? 'bg-emerald-400 animate-pulse' : 'bg-amber-400'"></span>
              {{ signalR.status() === 'connected' ? 'Live Stream Active' : 'Connecting...' }}
            </span>
          </div>
          <h1 class="text-2xl font-bold text-white mt-1">Realtime Sandbox Receiver & Simulator</h1>
          <p class="text-xs text-surface-400 mt-0.5">
            Test and debug inbound webhooks in isolation. Inspect headers and payloads live, and simulate gateway responses with latency & custom status codes.
          </p>
        </div>

        <!-- Sandbox Action Buttons -->
        <div class="flex items-center gap-3">
          <button
            (click)="openCreateModal()"
            class="px-4 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold shadow-lg shadow-indigo-600/30 transition-all flex items-center gap-2">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
            </svg>
            <span>New Sandbox</span>
          </button>
          <button
            (click)="loadSandboxes()"
            [disabled]="loadingSandboxes()"
            class="p-2 rounded-xl bg-surface-800 hover:bg-surface-700 text-surface-300 hover:text-white border border-surface-700 transition-colors"
            title="Refresh Sandboxes">
            <svg class="w-4 h-4" [class.animate-spin]="loadingSandboxes()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
            </svg>
          </button>
        </div>
      </div>

      <!-- Sandbox Selector & Receiver Endpoint Bar -->
      @if (sandboxes().length > 0) {
        <div class="bg-surface-900/90 p-5 rounded-2xl border border-surface-800 shadow-md space-y-4">
          <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
            <!-- Sandbox Selector Dropdown -->
            <div class="flex items-center gap-3">
              <label class="text-xs font-semibold text-surface-400 shrink-0">Active Sandbox:</label>
              <select
                [ngModel]="selectedSandboxId()"
                (ngModelChange)="onSelectSandbox($event)"
                class="bg-surface-950 text-white text-xs font-medium px-3.5 py-2 rounded-xl border border-surface-700 focus:outline-none focus:border-indigo-500 min-w-[200px]">
                @for (box of sandboxes(); track box.id) {
                  <option [value]="box.id">
                    {{ box.name }} ({{ box.slug }})
                  </option>
                }
              </select>

              @if (currentSandbox()) {
                <span
                  class="px-2.5 py-1 rounded-lg text-[11px] font-semibold"
                  [ngClass]="currentSandbox()!.isActive ? 'bg-emerald-500/15 text-emerald-300 border border-emerald-500/30' : 'bg-rose-500/15 text-rose-300 border border-rose-500/30'">
                  {{ currentSandbox()!.isActive ? 'Active' : 'Paused' }}
                </span>
                @if (currentSandbox()!.expiresAt) {
                  <span class="text-[11px] font-mono text-amber-400 bg-amber-500/10 px-2 py-0.5 rounded border border-amber-500/20">
                    Expires: {{ currentSandbox()!.expiresAt | date:'short' }}
                  </span>
                }
              }
            </div>

            <!-- Danger / Management Actions -->
            @if (currentSandbox()) {
              <div class="flex items-center gap-2">
                <button
                  (click)="clearRequests()"
                  [disabled]="clearingRequests() || requests().length === 0"
                  class="px-3 py-1.5 rounded-lg bg-surface-800 hover:bg-surface-700 disabled:opacity-50 text-surface-300 hover:text-white text-xs font-medium border border-surface-700 transition-colors flex items-center gap-1.5">
                  <svg class="w-3.5 h-3.5 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/>
                  </svg>
                  <span>Clear Captured ({{ requests().length }})</span>
                </button>
                <button
                  (click)="deleteSandbox()"
                  class="px-3 py-1.5 rounded-lg bg-rose-950/40 hover:bg-rose-900/60 text-rose-300 hover:text-white text-xs font-medium border border-rose-800/40 transition-colors flex items-center gap-1.5">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
                  </svg>
                  <span>Delete Sandbox</span>
                </button>
              </div>
            }
          </div>

          <!-- Receiver URL & Quick cURL Generator -->
          @if (currentSandbox()) {
            <div class="bg-surface-950 p-4 rounded-xl border border-surface-800/80 space-y-3">
              <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div class="flex items-center gap-2 overflow-hidden">
                  <span class="text-xs font-semibold text-indigo-400 uppercase tracking-wider shrink-0">Receiver URL:</span>
                  <code class="text-xs font-mono text-surface-200 bg-surface-900 px-2.5 py-1 rounded border border-surface-800 truncate select-all">
                    {{ currentSandbox()!.receiverUrl }}
                  </code>
                </div>

                <div class="flex items-center gap-2 shrink-0">
                  <button
                    (click)="copyReceiverUrl()"
                    class="px-3 py-1.5 rounded-lg bg-indigo-600/20 hover:bg-indigo-600/30 text-indigo-300 border border-indigo-500/40 text-xs font-medium transition-colors flex items-center gap-1.5">
                    @if (urlCopied()) {
                      <svg class="w-3.5 h-3.5 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/>
                      </svg>
                      <span class="text-emerald-300">Copied!</span>
                    } @else {
                      <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 002-2M8 5a2 2 0 012-2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10a2 2 0 00-2 2v3a2 2 0 002 2h10a2 2 0 002-2v-3a2 2 0 00-2-2z"/>
                      </svg>
                      <span>Copy URL</span>
                    }
                  </button>
                  <button
                    (click)="showCurlExample.set(!showCurlExample())"
                    class="px-3 py-1.5 rounded-lg bg-surface-800 hover:bg-surface-700 text-surface-300 hover:text-white text-xs font-medium border border-surface-700 transition-colors">
                    {{ showCurlExample() ? 'Hide cURL' : 'cURL Example' }}
                  </button>
                </div>
              </div>

              <!-- Collapsible cURL Snippet -->
              @if (showCurlExample()) {
                <div class="mt-2 pt-2 border-t border-surface-800/80">
                  <app-code-viewer [code]="curlSnippet()" language="bash" title="Send a Test Webhook via cURL"></app-code-viewer>
                </div>
              }
            </div>
          }
        </div>
      } @else if (!loadingSandboxes()) {
        <!-- Empty Sandboxes State -->
        <div class="bg-surface-900/60 p-12 rounded-2xl border border-surface-800 text-center space-y-4">
          <div class="w-16 h-16 bg-indigo-500/10 text-indigo-400 rounded-2xl flex items-center justify-center mx-auto border border-indigo-500/20">
            <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M19.428 15.428a2 2 0 00-1.022-.547l-2.387-.477a6 6 0 00-3.86.517l-.318.158a6 6 0 01-3.86.517L6.05 15.21a2 2 0 00-1.806.547M8 4h8l-1 1v5.172a2 2 0 00.586 1.414l5 5c1.26 1.26.367 3.414-1.415 3.414H4.828c-1.782 0-2.674-2.154-1.414-3.414l5-5A2 2 0 009 10.172V5L8 4z"/>
            </svg>
          </div>
          <div>
            <h3 class="text-lg font-bold text-white">No Webhook Sandboxes Created Yet</h3>
            <p class="text-xs text-surface-400 max-w-md mx-auto mt-1">
              Create an ephemeral or persistent sandbox endpoint to receive webhooks from Stripe, GitHub, Shopify, or any third-party provider without setting up ngrok or exposing local ports.
            </p>
          </div>
          <button
            (click)="openCreateModal()"
            class="px-4 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold shadow-lg shadow-indigo-600/30 transition-all inline-flex items-center gap-2">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
            </svg>
            <span>Create Your First Sandbox</span>
          </button>
        </div>
      }

      <!-- Main Two-Column Layout (Requests Stream on Left, Simulator & Details on Right) -->
      @if (currentSandbox()) {
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
          <!-- Left Column: Captured Requests Timeline (7 Cols) -->
          <div class="lg:col-span-7 space-y-4">
            <!-- Filter & Search Bar -->
            <div class="bg-surface-900/90 p-4 rounded-xl border border-surface-800 flex flex-wrap items-center justify-between gap-3">
              <div class="flex items-center gap-2">
                <span class="text-xs font-semibold text-white">Captured Requests</span>
                <span class="text-xs font-mono bg-surface-800 text-indigo-300 px-2 py-0.5 rounded-full border border-surface-700">
                  {{ requests().length }}
                </span>
              </div>

              <div class="flex items-center gap-2">
                <!-- Method Filter -->
                <select
                  [(ngModel)]="filterMethod"
                  (ngModelChange)="loadRequests()"
                  class="bg-surface-950 text-surface-300 text-xs px-2.5 py-1.5 rounded-lg border border-surface-700 focus:outline-none focus:border-indigo-500">
                  <option value="">All Methods</option>
                  <option value="POST">POST</option>
                  <option value="GET">GET</option>
                  <option value="PUT">PUT</option>
                  <option value="DELETE">DELETE</option>
                  <option value="PATCH">PATCH</option>
                </select>

                <!-- Search Input -->
                <input
                  type="text"
                  [(ngModel)]="searchQuery"
                  (keyup.enter)="loadRequests()"
                  placeholder="Search payload/path..."
                  class="bg-surface-950 text-surface-200 text-xs px-3 py-1.5 rounded-lg border border-surface-700 focus:outline-none focus:border-indigo-500 w-36 sm:w-48"/>

                <button
                  (click)="loadRequests()"
                  class="p-1.5 bg-surface-800 hover:bg-surface-700 text-surface-300 hover:text-white rounded-lg border border-surface-700 transition-colors"
                  title="Search">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
                  </svg>
                </button>
              </div>
            </div>

            <!-- Requests List -->
            @if (loadingRequests()) {
              <div class="space-y-3">
                <app-skeleton-loader customClass="h-16 w-full rounded-xl"></app-skeleton-loader>
                <app-skeleton-loader customClass="h-16 w-full rounded-xl"></app-skeleton-loader>
                <app-skeleton-loader customClass="h-16 w-full rounded-xl"></app-skeleton-loader>
              </div>
            } @else if (requests().length === 0) {
              <div class="bg-surface-900/40 border border-dashed border-surface-800 rounded-2xl p-10 text-center space-y-3">
                <div class="w-12 h-12 rounded-xl bg-surface-800/80 text-surface-400 flex items-center justify-center mx-auto">
                  <svg class="w-6 h-6 animate-pulse text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/>
                  </svg>
                </div>
                <div>
                  <h4 class="text-sm font-semibold text-surface-200">Listening for Webhooks...</h4>
                  <p class="text-xs text-surface-400 max-w-sm mx-auto mt-0.5">
                    Send an HTTP request to the receiver URL above. Incoming requests will appear in real time automatically.
                  </p>
                </div>
              </div>
            } @else {
              <div class="space-y-2.5">
                @for (req of requests(); track req.id) {
                  <div
                    (click)="selectRequest(req.id)"
                    class="p-3.5 rounded-xl border transition-all cursor-pointer flex items-center justify-between gap-3 group"
                    [ngClass]="selectedRequestId() === req.id ? 'bg-indigo-950/40 border-indigo-500/50 shadow-md shadow-indigo-950/30' : 'bg-surface-900/80 border-surface-800 hover:border-surface-700 hover:bg-surface-900'">
                    
                    <div class="flex items-center gap-3 min-w-0">
                      <!-- Method Badge -->
                      <span
                        class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider shrink-0 font-mono"
                        [ngClass]="getMethodClass(req.httpMethod)">
                        {{ req.httpMethod }}
                      </span>

                      <!-- Path & Content Type -->
                      <div class="min-w-0">
                        <div class="text-xs font-mono text-surface-200 truncate group-hover:text-indigo-300 transition-colors">
                          {{ req.path }}
                        </div>
                        <div class="text-[10px] text-surface-400 flex items-center gap-2 mt-0.5">
                          <span>{{ req.receivedAt | date:'mediumTime' }}</span>
                          <span>&bull;</span>
                          <span>{{ req.contentLength }} bytes</span>
                          @if (req.clientIp) {
                            <span>&bull;</span>
                            <span class="font-mono text-surface-400">{{ req.clientIp }}</span>
                          }
                        </div>
                      </div>
                    </div>

                    <!-- Response Simulation Badges -->
                    <div class="flex items-center gap-2 shrink-0">
                      <span
                        class="px-2 py-0.5 rounded text-[11px] font-mono font-semibold"
                        [ngClass]="getStatusBadgeClass(req.responseStatusCode)">
                        {{ req.responseStatusCode }}
                      </span>
                      <span class="text-[11px] font-mono text-surface-400">
                        {{ req.durationMs | number:'1.0-0' }}ms
                      </span>
                      <svg class="w-4 h-4 text-surface-500 group-hover:text-surface-300 transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/>
                      </svg>
                    </div>
                  </div>
                }
              </div>
            }
          </div>

          <!-- Right Column: Response Simulator & Request Inspector (5 Cols) -->
          <div class="lg:col-span-5 space-y-6">
            <!-- 1. Configurable Response Simulator -->
            <div class="bg-surface-900/95 p-5 rounded-2xl border border-surface-800 shadow-lg space-y-4">
              <div class="flex items-center justify-between pb-3 border-b border-surface-800">
                <div class="flex items-center gap-2">
                  <div class="w-2 h-2 rounded-full bg-indigo-400"></div>
                  <h3 class="text-sm font-bold text-white">Response Simulator</h3>
                </div>
                <div class="flex items-center gap-2">
                  <label class="text-xs text-surface-400">Status:</label>
                  <button
                    (click)="simActive.set(!simActive())"
                    class="px-2.5 py-0.5 rounded-full text-xs font-semibold transition-colors"
                    [ngClass]="simActive() ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30' : 'bg-rose-500/20 text-rose-300 border border-rose-500/30'">
                    {{ simActive() ? 'Enabled' : 'Paused' }}
                  </button>
                </div>
              </div>

              <!-- Status Code & Latency Inputs -->
              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-[11px] font-semibold text-surface-400 mb-1">Status Code</label>
                  <select
                    [(ngModel)]="simStatusCode"
                    class="w-full bg-surface-950 text-surface-200 text-xs px-3 py-2 rounded-xl border border-surface-700 focus:outline-none focus:border-indigo-500 font-mono">
                    <option [value]="200">200 OK</option>
                    <option [value]="201">201 Created</option>
                    <option [value]="202">202 Accepted</option>
                    <option [value]="204">204 No Content</option>
                    <option [value]="400">400 Bad Request</option>
                    <option [value]="401">401 Unauthorized</option>
                    <option [value]="404">404 Not Found</option>
                    <option [value]="429">429 Rate Limited</option>
                    <option [value]="500">500 Server Error</option>
                    <option [value]="503">503 Unavailable</option>
                  </select>
                </div>

                <div>
                  <label class="block text-[11px] font-semibold text-surface-400 mb-1">
                    Latency Delay: <span class="text-indigo-400 font-mono">{{ simDelayMs }}ms</span>
                  </label>
                  <input
                    type="range"
                    min="0"
                    max="5000"
                    step="50"
                    [(ngModel)]="simDelayMs"
                    class="w-full accent-indigo-500 bg-surface-950 h-2 rounded-lg cursor-pointer mt-2"/>
                </div>
              </div>

              <!-- Content-Type Selector -->
              <div>
                <label class="block text-[11px] font-semibold text-surface-400 mb-1">Content-Type</label>
                <select
                  [(ngModel)]="simContentType"
                  class="w-full bg-surface-950 text-surface-200 text-xs px-3 py-2 rounded-xl border border-surface-700 focus:outline-none focus:border-indigo-500 font-mono">
                  <option value="application/json">application/json</option>
                  <option value="text/plain">text/plain</option>
                  <option value="application/xml">application/xml</option>
                </select>
              </div>

              <!-- Simulated Response Body -->
              <div>
                <div class="flex items-center justify-between mb-1">
                  <label class="text-[11px] font-semibold text-surface-400">Response Body</label>
                  <button
                    (click)="formatSimJson()"
                    class="text-[10px] text-indigo-400 hover:text-indigo-300">
                    Format JSON
                  </button>
                </div>
                <textarea
                  [(ngModel)]="simBody"
                  rows="4"
                  placeholder='{"status": "ok", "received": true}'
                  class="w-full bg-surface-950 text-surface-200 text-xs font-mono p-3 rounded-xl border border-surface-700 focus:outline-none focus:border-indigo-500 resize-none">
                </textarea>
              </div>

              <!-- Save Simulation Rules Button -->
              <button
                (click)="saveSimulationRules()"
                [disabled]="savingRules()"
                class="w-full py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white text-xs font-semibold shadow-md shadow-indigo-600/30 transition-all flex items-center justify-center gap-2">
                @if (savingRules()) {
                  <svg class="w-4 h-4 animate-spin" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
                  </svg>
                  <span>Saving Rules...</span>
                } @else {
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/>
                  </svg>
                  <span>Save Simulation Rules</span>
                }
              </button>
            </div>

            <!-- 2. Deep Request Inspector (Selected Request) -->
            @if (selectedRequestDetail()) {
              <div class="bg-surface-900/95 p-5 rounded-2xl border border-surface-800 shadow-lg space-y-4">
                <div class="flex items-center justify-between pb-3 border-b border-surface-800">
                  <div class="flex items-center gap-2">
                    <span
                      class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider font-mono"
                      [ngClass]="getMethodClass(selectedRequestDetail()!.httpMethod)">
                      {{ selectedRequestDetail()!.httpMethod }}
                    </span>
                    <h3 class="text-xs font-mono text-white truncate max-w-[200px]">
                      {{ selectedRequestDetail()!.path }}
                    </h3>
                  </div>

                  <button
                    (click)="selectedRequestId.set(null); selectedRequestDetail.set(null)"
                    class="text-surface-400 hover:text-white p-1 rounded hover:bg-surface-800 transition-colors">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
                    </svg>
                  </button>
                </div>

                <!-- Request Metadata Cards -->
                <div class="grid grid-cols-2 sm:grid-cols-4 gap-2 text-center">
                  <div class="bg-surface-950 p-2.5 rounded-xl border border-surface-800">
                    <div class="text-[10px] text-surface-400 uppercase">Response</div>
                    <div class="text-xs font-mono font-bold" [ngClass]="getStatusBadgeClass(selectedRequestDetail()!.responseStatusCode)">
                      {{ selectedRequestDetail()!.responseStatusCode }}
                    </div>
                  </div>
                  <div class="bg-surface-950 p-2.5 rounded-xl border border-surface-800">
                    <div class="text-[10px] text-surface-400 uppercase">Latency</div>
                    <div class="text-xs font-mono font-bold text-surface-200">
                      {{ selectedRequestDetail()!.durationMs | number:'1.0-0' }}ms
                    </div>
                  </div>
                  <div class="bg-surface-950 p-2.5 rounded-xl border border-surface-800">
                    <div class="text-[10px] text-surface-400 uppercase">Payload Size</div>
                    <div class="text-xs font-mono font-bold text-surface-200">
                      {{ selectedRequestDetail()!.contentLength }} B
                    </div>
                  </div>
                  <div class="bg-surface-950 p-2.5 rounded-xl border border-surface-800">
                    <div class="text-[10px] text-surface-400 uppercase">Client IP</div>
                    <div class="text-xs font-mono font-bold text-surface-200 truncate">
                      {{ selectedRequestDetail()!.clientIp || 'Unknown' }}
                    </div>
                  </div>
                </div>

                <!-- Query Parameters if present -->
                @if (selectedRequestDetail()!.queryString) {
                  <div>
                    <span class="text-[11px] font-semibold text-surface-400 block mb-1">Query String</span>
                    <code class="text-xs font-mono text-indigo-300 bg-surface-950 p-2 rounded-lg border border-surface-800 block truncate">
                      {{ selectedRequestDetail()!.queryString }}
                    </code>
                  </div>
                }

                <!-- Request Tabs (Headers vs Body) -->
                <div class="space-y-3">
                  <div class="flex items-center gap-2 border-b border-surface-800">
                    <button
                      (click)="inspectorTab.set('body')"
                      [class.border-indigo-500]="inspectorTab() === 'body'"
                      [class.text-indigo-300]="inspectorTab() === 'body'"
                      [class.text-surface-400]="inspectorTab() !== 'body'"
                      class="px-3 py-1.5 text-xs font-semibold border-b-2 border-transparent transition-colors">
                      Payload Body
                    </button>
                    <button
                      (click)="inspectorTab.set('headers')"
                      [class.border-indigo-500]="inspectorTab() === 'headers'"
                      [class.text-indigo-300]="inspectorTab() === 'headers'"
                      [class.text-surface-400]="inspectorTab() !== 'headers'"
                      class="px-3 py-1.5 text-xs font-semibold border-b-2 border-transparent transition-colors">
                      Headers
                    </button>
                  </div>

                  @if (inspectorTab() === 'body') {
                    <app-code-viewer
                      [code]="selectedRequestDetail()!.body || '{}'"
                      language="json"
                      title="Captured Webhook Body">
                    </app-code-viewer>
                  } @else {
                    <app-code-viewer
                      [code]="selectedRequestDetail()!.headersJson"
                      language="json"
                      title="Captured HTTP Request Headers">
                    </app-code-viewer>
                  }
                </div>
              </div>
            }
          </div>
        </div>
      }
    </div>

    <!-- Create Sandbox Modal -->
    @if (showCreateModal()) {
      <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm animate-in fade-in duration-200">
        <div class="bg-surface-900 border border-surface-800 rounded-2xl w-full max-w-lg p-6 shadow-2xl space-y-4">
          <div class="flex items-center justify-between pb-3 border-b border-surface-800">
            <div>
              <h3 class="text-base font-bold text-white">Create New Webhook Sandbox</h3>
              <p class="text-xs text-surface-400">Provision an isolated receiver endpoint with response simulator.</p>
            </div>
            <button
              (click)="showCreateModal.set(false)"
              class="text-surface-400 hover:text-white p-1 rounded hover:bg-surface-800 transition-colors">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          </div>

          <div class="space-y-3">
            <div>
              <label class="block text-xs font-semibold text-surface-300 mb-1">Sandbox Name *</label>
              <input
                type="text"
                [(ngModel)]="newSandboxName"
                placeholder="e.g. Stripe Webhook Testing"
                class="w-full bg-surface-950 text-surface-200 text-xs px-3.5 py-2.5 rounded-xl border border-surface-700 focus:outline-none focus:border-indigo-500"/>
            </div>

            <div>
              <label class="block text-xs font-semibold text-surface-300 mb-1">Custom Slug (Optional)</label>
              <div class="flex items-center">
                <span class="bg-surface-800 text-surface-400 text-xs px-3 py-2.5 rounded-l-xl border border-r-0 border-surface-700 font-mono">sb_</span>
                <input
                  type="text"
                  [(ngModel)]="newSandboxSlug"
                  placeholder="payment-events"
                  class="w-full bg-surface-950 text-surface-200 text-xs px-3.5 py-2.5 rounded-r-xl border border-surface-700 focus:outline-none focus:border-indigo-500 font-mono"/>
              </div>
              <span class="text-[10px] text-surface-400 mt-1 block">Leave empty to auto-generate a secure random slug.</span>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-xs font-semibold text-surface-300 mb-1">Default Status Code</label>
                <input
                  type="number"
                  [(ngModel)]="newSandboxStatusCode"
                  class="w-full bg-surface-950 text-surface-200 text-xs px-3.5 py-2.5 rounded-xl border border-surface-700 focus:outline-none focus:border-indigo-500 font-mono"/>
              </div>

              <div>
                <label class="block text-xs font-semibold text-surface-300 mb-1">Auto-Expire (TTL)</label>
                <select
                  [(ngModel)]="newSandboxTtlHours"
                  class="w-full bg-surface-950 text-surface-200 text-xs px-3.5 py-2.5 rounded-xl border border-surface-700 focus:outline-none focus:border-indigo-500">
                  <option [ngValue]="null">Never (Persistent)</option>
                  <option [ngValue]="1">1 Hour</option>
                  <option [ngValue]="6">6 Hours</option>
                  <option [ngValue]="24">24 Hours</option>
                  <option [ngValue]="168">7 Days</option>
                </select>
              </div>
            </div>

            <div>
              <label class="block text-xs font-semibold text-surface-300 mb-1">Initial Response Body</label>
              <textarea
                [(ngModel)]="newSandboxBody"
                rows="3"
                placeholder='{"status": "ok", "received": true}'
                class="w-full bg-surface-950 text-surface-200 text-xs font-mono p-3 rounded-xl border border-surface-700 focus:outline-none focus:border-indigo-500 resize-none">
              </textarea>
            </div>
          </div>

          <div class="flex items-center justify-end gap-3 pt-3 border-t border-surface-800">
            <button
              (click)="showCreateModal.set(false)"
              class="px-4 py-2 rounded-xl bg-surface-800 hover:bg-surface-700 text-surface-300 hover:text-white text-xs font-semibold transition-colors">
              Cancel
            </button>
            <button
              (click)="createSandbox()"
              [disabled]="creatingSandbox() || !newSandboxName.trim()"
              class="px-4 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white text-xs font-semibold shadow-lg shadow-indigo-600/30 transition-all flex items-center gap-2">
              @if (creatingSandbox()) {
                <svg class="w-4 h-4 animate-spin" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
                </svg>
                <span>Creating...</span>
              } @else {
                <span>Create Sandbox</span>
              }
            </button>
          </div>
        </div>
      </div>
    }
  `
})
export class SandboxComponent implements OnInit, OnDestroy {
  private readonly destroyRef = inject(DestroyRef);
  private readonly sandboxService = inject(SandboxService);
  readonly signalR = inject(SignalRService);
  private readonly toast = inject(ToastService);

  // Sandboxes State
  readonly sandboxes = signal<WebhookSandbox[]>([]);
  readonly selectedSandboxId = signal<string | null>(null);
  readonly loadingSandboxes = signal<boolean>(false);
  readonly currentSandbox = computed(() => {
    const id = this.selectedSandboxId();
    return this.sandboxes().find(s => s.id === id) ?? null;
  });

  // Captured Requests State
  readonly requests = signal<SandboxRequestSummary[]>([]);
  readonly loadingRequests = signal<boolean>(false);
  readonly clearingRequests = signal<boolean>(false);
  readonly selectedRequestId = signal<string | null>(null);
  readonly selectedRequestDetail = signal<SandboxRequestDetail | null>(null);
  readonly inspectorTab = signal<'body' | 'headers'>('body');

  // Filters
  filterMethod = '';
  searchQuery = '';

  // UI State
  readonly showCreateModal = signal<boolean>(false);
  readonly creatingSandbox = signal<boolean>(false);
  readonly urlCopied = signal<boolean>(false);
  readonly showCurlExample = signal<boolean>(false);

  // Response Simulator Edit State
  simStatusCode = 200;
  simDelayMs = 0;
  simContentType = 'application/json';
  simBody = '{"status": "ok", "received": true}';
  readonly simActive = signal<boolean>(true);
  readonly savingRules = signal<boolean>(false);

  // Create Modal Fields
  newSandboxName = '';
  newSandboxSlug = '';
  newSandboxStatusCode = 200;
  newSandboxTtlHours: number | null = 24;
  newSandboxBody = '{"status": "ok", "received": true}';

  readonly curlSnippet = computed(() => {
    const sb = this.currentSandbox();
    if (!sb) return '';
    return `curl -X POST "${sb.receiverUrl}" \\\n  -H "Content-Type: application/json" \\\n  -H "X-Webhook-Event: payment.succeeded" \\\n  -d '{"event":"payment.succeeded","amount":1500,"currency":"usd"}'`;
  });

  constructor() {
    // Reactive SignalR event listener for new sandbox requests
    effect(() => {
      const evt = this.signalR.latestSandboxEvent();
      if (!evt) return;

      const current = this.currentSandbox();
      if (current && evt.sandboxId === current.id) {
        // Prepend new request
        const summary: SandboxRequestSummary = {
          id: evt.request.id,
          sandboxId: evt.request.sandboxId,
          httpMethod: evt.request.httpMethod,
          path: evt.request.path,
          contentType: evt.request.contentType,
          contentLength: evt.request.contentLength,
          clientIp: evt.request.clientIp,
          responseStatusCode: evt.request.responseStatusCode,
          responseDelayMs: evt.request.responseDelayMs,
          receivedAt: evt.request.receivedAt,
          durationMs: evt.request.durationMs
        };

        this.requests.update(list => [summary, ...list]);
        this.toast.show({
          title: `Captured ${evt.request.httpMethod} Request`,
          message: `${evt.request.path} (${evt.request.responseStatusCode} simulated)`,
          type: 'info'
        });
      }
    });

    // Reactive SignalR event listener for sandbox cleared
    effect(() => {
      const clearedId = this.signalR.clearedSandboxId();
      if (!clearedId) return;

      if (this.selectedSandboxId() === clearedId) {
        this.requests.set([]);
        this.selectedRequestId.set(null);
        this.selectedRequestDetail.set(null);
      }
    });
  }

  async ngOnInit(): Promise<void> {
    await this.signalR.startConnection();
    this.loadSandboxes();
  }

  ngOnDestroy(): void {
    // Component tear down
  }

  loadSandboxes(): void {
    this.loadingSandboxes.set(true);
    this.sandboxService.getSandboxes().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (list) => {
        this.sandboxes.set(list);
        this.loadingSandboxes.set(false);

        if (list.length > 0) {
          const currentId = this.selectedSandboxId();
          if (!currentId || !list.some(s => s.id === currentId)) {
            this.onSelectSandbox(list[0].id);
          } else {
            this.syncSimulatorState();
          }
        } else {
          this.selectedSandboxId.set(null);
          this.requests.set([]);
        }
      },
      error: (err) => {
        this.loadingSandboxes.set(false);
        this.toast.show({
          title: 'Error loading sandboxes',
          message: err?.error?.detail || err?.message || 'Could not fetch webhook sandboxes.',
          type: 'error'
        });
      }
    });
  }

  onSelectSandbox(sandboxId: string): void {
    this.selectedSandboxId.set(sandboxId);
    this.selectedRequestId.set(null);
    this.selectedRequestDetail.set(null);
    this.syncSimulatorState();
    this.loadRequests();
  }

  private syncSimulatorState(): void {
    const sb = this.currentSandbox();
    if (!sb) return;

    this.simStatusCode = sb.defaultResponseStatusCode;
    this.simDelayMs = sb.defaultResponseDelayMs;
    this.simContentType = sb.defaultResponseContentType || 'application/json';
    this.simBody = sb.defaultResponseBody || '{"status": "ok", "received": true}';
    this.simActive.set(sb.isActive);
  }

  loadRequests(): void {
    const sb = this.currentSandbox();
    if (!sb) return;

    this.loadingRequests.set(true);
    this.sandboxService.getSandboxRequests(sb.id, {
      method: this.filterMethod || undefined,
      search: this.searchQuery.trim() || undefined,
      page: 1,
      pageSize: 50
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (paged) => {
        this.requests.set(paged.items);
        this.loadingRequests.set(false);
      },
      error: () => {
        this.loadingRequests.set(false);
      }
    });
  }

  selectRequest(requestId: string): void {
    const sb = this.currentSandbox();
    if (!sb) return;

    this.selectedRequestId.set(requestId);
    this.sandboxService.getSandboxRequestById(sb.id, requestId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (detail) => {
        this.selectedRequestDetail.set(detail);
      },
      error: (err) => {
        this.toast.show({
          title: 'Error fetching request details',
          message: err?.error?.detail || 'Could not inspect captured webhook request.',
          type: 'error'
        });
      }
    });
  }

  saveSimulationRules(): void {
    const sb = this.currentSandbox();
    if (!sb) return;

    this.savingRules.set(true);
    const command: UpdateSandboxConfigRequest = {
      name: sb.name,
      defaultStatusCode: Number(this.simStatusCode),
      defaultDelayMs: Number(this.simDelayMs),
      defaultContentType: this.simContentType,
      defaultBody: this.simBody,
      isActive: this.simActive()
    };

    this.sandboxService.updateSandbox(sb.id, command).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (updated) => {
        this.savingRules.set(false);
        this.sandboxes.update(list => list.map(s => s.id === updated.id ? updated : s));
        this.toast.show({
          title: 'Simulation Rules Saved',
          message: `Responding with HTTP ${updated.defaultResponseStatusCode} and ${updated.defaultResponseDelayMs}ms latency.`,
          type: 'success'
        });
      },
      error: (err) => {
        this.savingRules.set(false);
        this.toast.show({
          title: 'Failed to update rules',
          message: err?.error?.detail || 'Could not save simulation config.',
          type: 'error'
        });
      }
    });
  }

  clearRequests(): void {
    const sb = this.currentSandbox();
    if (!sb) return;

    this.clearingRequests.set(true);
    this.sandboxService.clearSandboxRequests(sb.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.clearingRequests.set(false);
        this.requests.set([]);
        this.selectedRequestId.set(null);
        this.selectedRequestDetail.set(null);
        this.toast.show({
          title: 'Requests Cleared',
          message: 'All captured request history has been purged for this sandbox.',
          type: 'success'
        });
      },
      error: (err) => {
        this.clearingRequests.set(false);
        this.toast.show({
          title: 'Failed to clear requests',
          message: err?.error?.detail || 'Could not clear requests.',
          type: 'error'
        });
      }
    });
  }

  deleteSandbox(): void {
    const sb = this.currentSandbox();
    if (!sb) return;

    if (!confirm(`Are you sure you want to delete sandbox "${sb.name}"? This action cannot be undone.`)) {
      return;
    }

    this.sandboxService.deleteSandbox(sb.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.sandboxes.update(list => list.filter(s => s.id !== sb.id));
        const remaining = this.sandboxes();
        if (remaining.length > 0) {
          this.onSelectSandbox(remaining[0].id);
        } else {
          this.selectedSandboxId.set(null);
          this.requests.set([]);
        }
        this.toast.show({
          title: 'Sandbox Deleted',
          message: `Sandbox "${sb.name}" has been permanently removed.`,
          type: 'success'
        });
      },
      error: (err) => {
        this.toast.show({
          title: 'Failed to delete sandbox',
          message: err?.error?.detail || 'Could not delete sandbox.',
          type: 'error'
        });
      }
    });
  }

  openCreateModal(): void {
    this.newSandboxName = '';
    this.newSandboxSlug = '';
    this.newSandboxStatusCode = 200;
    this.newSandboxTtlHours = 24;
    this.newSandboxBody = '{"status": "ok", "received": true}';
    this.showCreateModal.set(true);
  }

  createSandbox(): void {
    if (!this.newSandboxName.trim()) return;

    this.creatingSandbox.set(true);
    const command: CreateSandboxRequest = {
      name: this.newSandboxName.trim(),
      customSlug: this.newSandboxSlug.trim() ? this.newSandboxSlug.trim() : null,
      defaultStatusCode: Number(this.newSandboxStatusCode),
      defaultDelayMs: 0,
      defaultContentType: 'application/json',
      defaultBody: this.newSandboxBody,
      ttlHours: this.newSandboxTtlHours
    };

    this.sandboxService.createSandbox(command).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (created) => {
        this.creatingSandbox.set(false);
        this.showCreateModal.set(false);
        this.sandboxes.update(list => [created, ...list]);
        this.onSelectSandbox(created.id);
        this.toast.show({
          title: 'Sandbox Created',
          message: `Receiver URL ready at /receiver/${created.slug}`,
          type: 'success'
        });
      },
      error: (err) => {
        this.creatingSandbox.set(false);
        this.toast.show({
          title: 'Failed to create sandbox',
          message: err?.error?.detail || 'Could not create sandbox.',
          type: 'error'
        });
      }
    });
  }

  async copyReceiverUrl(): Promise<void> {
    const sb = this.currentSandbox();
    if (!sb) return;

    try {
      await navigator.clipboard.writeText(sb.receiverUrl);
      this.urlCopied.set(true);
      setTimeout(() => this.urlCopied.set(false), 2000);
    } catch {
      // Fallback
    }
  }

  formatSimJson(): void {
    try {
      const parsed = JSON.parse(this.simBody);
      this.simBody = JSON.stringify(parsed, null, 2);
    } catch {
      // ignore
    }
  }

  getMethodClass(method: string): string {
    switch (method?.toUpperCase()) {
      case 'POST': return 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40';
      case 'GET': return 'bg-sky-500/20 text-sky-300 border border-sky-500/40';
      case 'PUT': return 'bg-amber-500/20 text-amber-300 border border-amber-500/40';
      case 'DELETE': return 'bg-rose-500/20 text-rose-300 border border-rose-500/40';
      case 'PATCH': return 'bg-purple-500/20 text-purple-300 border border-purple-500/40';
      default: return 'bg-surface-700 text-surface-300 border border-surface-600';
    }
  }

  getStatusBadgeClass(statusCode: number): string {
    if (statusCode >= 200 && statusCode < 300) return 'text-emerald-400 bg-emerald-500/10 border border-emerald-500/20';
    if (statusCode >= 300 && statusCode < 400) return 'text-sky-400 bg-sky-500/10 border border-sky-500/20';
    if (statusCode >= 400 && statusCode < 500) return 'text-amber-400 bg-amber-500/10 border border-amber-500/20';
    return 'text-rose-400 bg-rose-500/10 border border-rose-500/20';
  }
}
