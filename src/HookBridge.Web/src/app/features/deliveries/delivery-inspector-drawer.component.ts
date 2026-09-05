import { Component, computed, inject, input, model, output, signal, effect, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SlideOverComponent } from '../../shared/components/ui/slide-over.component';
import { StatusBadgeComponent } from '../../shared/components/ui/status-badge.component';
import { CodeViewerComponent } from '../../shared/components/ui/code-viewer.component';
import { TabGroupComponent } from '../../shared/components/ui/tab-group.component';
import { TabComponent } from '../../shared/components/ui/tab.component';
import { SkeletonLoaderComponent } from '../../shared/components/ui/skeleton-loader.component';
import { ButtonComponent } from '../../shared/components/ui/button.component';
import { ModalComponent } from '../../shared/components/ui/modal.component';
import { DeliveryService, DeliveryDetail } from '../../core/services/delivery.service';
import { ToastService } from '../../shared/components/ui/toast/toast.service';
import { Endpoint, DeliveryAttempt } from '../../shared/models/control-plane.models';
import { RealtimeDeliveryEvent } from '../../core/signalr/models/signalr.models';

@Component({
  selector: 'app-delivery-inspector-drawer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    SlideOverComponent,
    StatusBadgeComponent,
    CodeViewerComponent,
    TabGroupComponent,
    TabComponent,
    SkeletonLoaderComponent,
    ButtonComponent,
    ModalComponent
  ],
  template: `
    <app-slide-over
      [isOpen]="isOpen()"
      [width]="'2xl'"
      (closed)="onClose()">
      
      <!-- Custom Header -->
      <div slot="title" class="flex items-center gap-3">
        <div class="p-2 rounded-lg bg-surface-800 border border-surface-700 text-brand-400">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/>
          </svg>
        </div>
        <div>
          <div class="flex items-center gap-2">
            <span class="text-base font-bold text-white font-mono">{{ displayEventType() }}</span>
            @if (currentStatus()) {
              <app-status-badge [status]="currentStatus()!"></app-status-badge>
            }
          </div>
          <div class="flex items-center gap-2 mt-0.5 text-xs text-surface-400 font-mono">
            <span>ID: {{ currentDeliveryId() }}</span>
            <button
              (click)="copyToClipboard(currentDeliveryId() || '', 'deliveryId')"
              class="p-0.5 text-surface-500 hover:text-surface-200 transition-colors"
              title="Copy Delivery ID">
              @if (copiedField() === 'deliveryId') {
                <span class="text-[10px] text-emerald-400 font-sans">Copied!</span>
              } @else {
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10a2 2 0 00-2 2v3a2 2 0 002 2h10a2 2 0 002-2v-3a2 2 0 00-2-2z"/>
                </svg>
              }
            </button>
          </div>
        </div>
      </div>

      <!-- Drawer Body Content -->
      @if (isLoading()) {
        <div class="space-y-4 py-6">
          <app-skeleton-loader customClass="h-16 w-full"></app-skeleton-loader>
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
          <div class="text-sm font-semibold text-white">Failed to load delivery details</div>
          <p class="text-xs text-surface-400 mt-1 max-w-sm mx-auto">{{ errorState() }}</p>
          <button (click)="retryLoad()" class="mt-4 px-3 py-1.5 bg-surface-800 hover:bg-surface-700 text-surface-200 text-xs rounded-lg transition-colors">
            Try Again
          </button>
        </div>
      } @else {
        <div class="space-y-6 pb-6">
          
          <!-- Key Meta Bar -->
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 p-3.5 bg-surface-950/60 rounded-xl border border-surface-800 text-xs">
            <div>
              <div class="text-[11px] text-surface-500 font-medium uppercase tracking-wider">Target Endpoint</div>
              <div class="text-surface-200 font-mono truncate mt-0.5" [title]="targetEndpointUrl()">
                {{ targetEndpointUrl() || 'Unknown Endpoint' }}
              </div>
            </div>
            <div>
              <div class="text-[11px] text-surface-500 font-medium uppercase tracking-wider">Attempts</div>
              <div class="text-surface-200 font-mono mt-0.5">
                {{ deliveryDetail()?.attemptCount || (attempts().length) || 0 }} total
              </div>
            </div>
            <div>
              <div class="text-[11px] text-surface-500 font-medium uppercase tracking-wider">Scheduled At</div>
              <div class="text-surface-300 font-mono mt-0.5">
                {{ (deliveryDetail()?.scheduledAt || realtimeEvent()?.timestamp) | date:'HH:mm:ss.SSS' }}
              </div>
            </div>
            <div>
              <div class="text-[11px] text-surface-500 font-medium uppercase tracking-wider">Correlation ID</div>
              <div class="text-surface-300 font-mono truncate mt-0.5 flex items-center gap-1" [title]="correlationId()">
                <span>{{ correlationId() }}</span>
                <button
                  (click)="copyToClipboard(correlationId(), 'corrId')"
                  class="text-surface-500 hover:text-surface-300">
                  <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10a2 2 0 00-2 2v3a2 2 0 002 2h10a2 2 0 002-2v-3a2 2 0 00-2-2z"/>
                  </svg>
                </button>
              </div>
            </div>
          </div>

          <!-- Lineage Warning Banner if Replayed -->
          @if (originalDeliveryId()) {
            <div class="flex items-center gap-2 p-3 bg-brand-950/40 border border-brand-800/60 rounded-xl text-xs text-brand-300">
              <svg class="w-4 h-4 text-brand-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
              </svg>
              <div>
                <span>This delivery was replayed from original delivery: </span>
                <span class="font-mono font-semibold">{{ originalDeliveryId() }}</span>
              </div>
            </div>
          }

          <!-- Tabs Section -->
          <app-tab-group [(activeTab)]="activeTab">
            
            <!-- Tab 1: Execution Timeline & Attempts -->
            <app-tab id="timeline" label="Timeline & Attempts" [badge]="attempts().length">
              <div class="space-y-4 pt-2">
                @if (attempts().length === 0) {
                  <div class="p-8 text-center border border-dashed border-surface-800 rounded-xl text-surface-500 text-xs">
                    No attempts recorded yet. The delivery is queued or pending dispatch.
                  </div>
                } @else {
                  <div class="relative pl-6 space-y-6 before:absolute before:left-2 before:top-3 before:bottom-3 before:w-0.5 before:bg-surface-800">
                    @for (att of attempts(); track att.id; let idx = $index) {
                      <div class="relative group">
                        <!-- Timeline Dot -->
                        <div
                          class="absolute -left-6 top-1.5 w-4.5 h-4.5 rounded-full border-2 flex items-center justify-center text-[9px] font-mono font-bold"
                          [ngClass]="getAttemptDotClasses(att)">
                          {{ att.attemptNumber }}
                        </div>

                        <!-- Attempt Card -->
                        <div
                          class="p-4 rounded-xl border transition-all cursor-pointer"
                          [ngClass]="selectedAttemptIndex() === idx ? 'bg-surface-900 border-brand-500/50 shadow-lg shadow-brand-500/5' : 'bg-surface-900/60 border-surface-800 hover:border-surface-700'"
                          (click)="selectAttempt(idx)">
                          
                          <div class="flex items-center justify-between">
                            <div class="flex items-center gap-2">
                              <span class="text-xs font-semibold text-white">Attempt #{{ att.attemptNumber }}</span>
                              
                              <!-- HTTP Status Badge -->
                              <span
                                class="px-2 py-0.5 rounded text-[11px] font-mono font-semibold"
                                [ngClass]="getHttpStatusClasses(att.httpStatusCode)">
                                {{ att.httpStatusCode ? 'HTTP ' + att.httpStatusCode : 'No Response' }}
                              </span>

                              <!-- Latency Badge -->
                              <span class="px-2 py-0.5 rounded bg-surface-800 text-surface-300 text-[11px] font-mono flex items-center gap-1">
                                <svg class="w-3 h-3 text-surface-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/>
                                </svg>
                                {{ att.elapsedMs }}ms
                              </span>
                            </div>

                            <span class="text-[11px] font-mono text-surface-500">
                              {{ att.executedAt | date:'HH:mm:ss.SSS' }}
                            </span>
                          </div>

                          <!-- Error Message if Failed -->
                          @if (att.errorMessage) {
                            <div class="mt-3 p-2.5 bg-rose-950/30 border border-rose-800/40 rounded-lg text-xs font-mono text-rose-300 flex items-start gap-2">
                              <svg class="w-4 h-4 text-rose-400 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
                              </svg>
                              <span class="break-all">{{ att.errorMessage }}</span>
                            </div>
                          }

                          <!-- Quick Snapshot of Attempt Payloads -->
                          @if (selectedAttemptIndex() === idx) {
                            <div class="mt-4 pt-4 border-t border-surface-800/80 space-y-3">
                              <div class="text-[11px] font-semibold text-surface-400 uppercase tracking-wider">Attempt Payload Snapshot</div>
                              <app-code-viewer
                                [code]="att.requestBody"
                                language="json"
                                title="Request Body">
                              </app-code-viewer>

                              @if (att.responseBody) {
                                <app-code-viewer
                                  [code]="att.responseBody"
                                  language="json"
                                  title="Response Body">
                                </app-code-viewer>
                              }
                            </div>
                          }
                        </div>
                      </div>
                    }
                  </div>
                }
              </div>
            </app-tab>

            <!-- Tab 2: Request Payload & Headers -->
            <app-tab id="request" label="Request">
              <div class="space-y-4 pt-2">
                <!-- Signature Header Section -->
                @if (signatureHeader()) {
                  <div class="p-3.5 bg-surface-950 rounded-xl border border-surface-800 space-y-2">
                    <div class="flex items-center justify-between">
                      <div class="flex items-center gap-1.5 text-xs font-semibold text-brand-300">
                        <svg class="w-4 h-4 text-brand-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/>
                        </svg>
                        <span>X-HookBridge-Signature (HMAC-SHA256)</span>
                      </div>
                      <button
                        (click)="copyToClipboard(signatureHeader()!, 'sig')"
                        class="text-[11px] text-surface-400 hover:text-white transition-colors flex items-center gap-1">
                        @if (copiedField() === 'sig') {
                          <span class="text-emerald-400">Copied</span>
                        } @else {
                          <span>Copy Signature</span>
                        }
                      </button>
                    </div>
                    <div class="font-mono text-xs text-surface-300 break-all bg-surface-900/80 p-2.5 rounded-lg border border-surface-800">
                      {{ signatureHeader() }}
                    </div>
                  </div>
                }

                <!-- Request Headers -->
                <div class="space-y-1.5">
                  <div class="text-[11px] font-semibold text-surface-400 uppercase tracking-wider">HTTP Request Headers</div>
                  <app-code-viewer
                    [code]="currentAttempt()?.requestHeadersJson || '{}'"
                    language="json"
                    title="Headers">
                  </app-code-viewer>
                </div>

                <!-- Request Body -->
                <div class="space-y-1.5">
                  <div class="flex items-center justify-between">
                    <div class="text-[11px] font-semibold text-surface-400 uppercase tracking-wider">Payload Body</div>
                    <span class="text-[10px] font-mono text-surface-500">{{ payloadByteSize() }} bytes</span>
                  </div>
                  <app-code-viewer
                    [code]="currentAttempt()?.requestBody || '{}'"
                    language="json"
                    title="JSON Payload">
                  </app-code-viewer>
                </div>
              </div>
            </app-tab>

            <!-- Tab 3: Response Data -->
            <app-tab id="response" label="Response">
              <div class="space-y-4 pt-2">
                @if (!currentAttempt() || !currentAttempt()?.httpStatusCode) {
                  <div class="p-8 text-center border border-dashed border-surface-800 rounded-xl text-surface-500 text-xs">
                    No HTTP response recorded for this attempt.
                  </div>
                } @else {
                  <!-- Response Summary -->
                  <div class="flex items-center gap-3 p-3 bg-surface-950 rounded-xl border border-surface-800 text-xs">
                    <div>
                      <span class="text-surface-500">Status:</span>
                      <span class="ml-1.5 font-mono font-bold" [ngClass]="getHttpStatusClasses(currentAttempt()?.httpStatusCode)">
                        {{ currentAttempt()?.httpStatusCode }}
                      </span>
                    </div>
                    <div>
                      <span class="text-surface-500">Latency:</span>
                      <span class="ml-1.5 font-mono text-surface-300 font-semibold">{{ currentAttempt()?.elapsedMs }}ms</span>
                    </div>
                  </div>

                  <!-- Response Headers -->
                  @if (currentAttempt()?.responseHeadersJson) {
                    <div class="space-y-1.5">
                      <div class="text-[11px] font-semibold text-surface-400 uppercase tracking-wider">Response Headers</div>
                      <app-code-viewer
                        [code]="currentAttempt()?.responseHeadersJson"
                        language="json"
                        title="Headers">
                      </app-code-viewer>
                    </div>
                  }

                  <!-- Response Body -->
                  <div class="space-y-1.5">
                    <div class="text-[11px] font-semibold text-surface-400 uppercase tracking-wider">Response Body</div>
                    <app-code-viewer
                      [code]="currentAttempt()?.responseBody || 'No response body returned.'"
                      [language]="'json'"
                      title="Body">
                    </app-code-viewer>
                  </div>
                }
              </div>
            </app-tab>

            <!-- Tab 4: cURL & CLI Generator -->
            <app-tab id="curl" label="cURL Command">
              <div class="space-y-4 pt-2">
                <div class="text-xs text-surface-400">
                  Reproduce or debug this exact webhook dispatch on your local machine using cURL:
                </div>
                <app-code-viewer
                  [code]="generatedCurlCommand()"
                  language="bash"
                  title="cURL Command">
                </app-code-viewer>
              </div>
            </app-tab>

            <!-- Tab 5: Tracing & Observability -->
            <app-tab id="trace" label="W3C Tracing">
              <div class="space-y-4 pt-2">
                <div class="p-4 bg-surface-950 rounded-xl border border-surface-800 space-y-3 text-xs">
                  <div class="text-[11px] font-semibold text-surface-400 uppercase tracking-wider">W3C Trace Context</div>
                  
                  <div class="grid grid-cols-1 gap-2.5 font-mono">
                    <div class="p-2 bg-surface-900 rounded border border-surface-800/80">
                      <span class="text-surface-500 text-[10px] block uppercase">Raw Traceparent</span>
                      <span class="text-surface-200 text-xs break-all">{{ traceParent() || 'None' }}</span>
                    </div>

                    <div class="grid grid-cols-2 gap-2">
                      <div class="p-2 bg-surface-900 rounded border border-surface-800/80">
                        <span class="text-surface-500 text-[10px] block uppercase">Trace ID</span>
                        <span class="text-brand-300 text-xs truncate block" [title]="parsedTrace().traceId">
                          {{ parsedTrace().traceId || 'N/A' }}
                        </span>
                      </div>
                      <div class="p-2 bg-surface-900 rounded border border-surface-800/80">
                        <span class="text-surface-500 text-[10px] block uppercase">Parent Span ID</span>
                        <span class="text-brand-300 text-xs truncate block" [title]="parsedTrace().spanId">
                          {{ parsedTrace().spanId || 'N/A' }}
                        </span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </app-tab>

          </app-tab-group>
        </div>
      }

      <!-- Drawer Footer Actions -->
      <div slot="footer" class="flex items-center justify-between w-full">
        <div class="flex items-center gap-2">
          <button
            (click)="openOverrideModal()"
            type="button"
            class="px-3 py-1.5 bg-surface-900 border border-surface-700 hover:bg-surface-800 text-surface-300 text-xs font-medium rounded-lg transition-colors">
            Replay to Alt Endpoint...
          </button>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="onClose()"
            type="button"
            class="px-3 py-1.5 bg-surface-900 border border-surface-700 hover:bg-surface-800 text-surface-300 text-xs font-medium rounded-lg transition-colors">
            Close
          </button>

          <app-button
            variant="primary"
            size="sm"
            [loading]="isReplaying()"
            [disabled]="isReplaying() || !currentDeliveryId()"
            (clicked)="triggerReplay()">
            <svg class="w-3.5 h-3.5 mr-1.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
            </svg>
            Replay Webhook
          </app-button>
        </div>
      </div>
    </app-slide-over>

    <!-- Override Endpoint Modal -->
    <app-modal
      [isOpen]="showOverrideModal()"
      title="Replay Webhook to Alternate Endpoint"
      (closed)="closeOverrideModal()">
      
      <div class="space-y-4 text-xs">
        <p class="text-surface-300">
          Redirect and replay this delivery payload with newly signed HMAC headers to a chosen active endpoint:
        </p>

        <div>
          <label class="block text-[11px] font-semibold text-surface-400 uppercase tracking-wider mb-1.5">
            Select Destination Endpoint
          </label>
          <select
            [(ngModel)]="overrideEndpointId"
            class="w-full bg-surface-950 border border-surface-800 rounded-lg p-2.5 text-white font-mono text-xs focus:border-brand-500 focus:outline-none">
            <option [ngValue]="null">-- Default (Original Endpoint) --</option>
            @for (ep of endpoints(); track ep.id) {
              <option [ngValue]="ep.id">
                {{ ep.targetUrl }} ({{ ep.status }})
              </option>
            }
          </select>
        </div>
      </div>

      <div slot="footer" class="flex items-center justify-end gap-3">
        <button
          (click)="closeOverrideModal()"
          class="px-3 py-1.5 bg-surface-800 hover:bg-surface-700 text-surface-300 text-xs rounded-lg transition-colors">
          Cancel
        </button>
        <app-button
          variant="primary"
          size="sm"
          [loading]="isReplaying()"
          (clicked)="triggerReplay(overrideEndpointId())">
          Execute Replay
        </app-button>
      </div>
    </app-modal>
  `
})
export class DeliveryInspectorDrawerComponent {
  private readonly deliveryService = inject(DeliveryService);
  private readonly toast = inject(ToastService);

  readonly isOpen = input<boolean>(false);
  readonly deliveryId = input<string | null>(null);
  readonly realtimeEvent = input<RealtimeDeliveryEvent | null>(null);
  readonly endpoints = input<Endpoint[]>([]);

  readonly closed = output<void>();
  readonly replayed = output<void>();

  readonly isLoading = signal<boolean>(false);
  readonly errorState = signal<string | null>(null);
  readonly deliveryDetail = signal<DeliveryDetail | null>(null);
  readonly selectedAttemptIndex = signal<number>(0);
  readonly activeTab = signal<string>('timeline');
  readonly isReplaying = signal<boolean>(false);
  readonly showOverrideModal = signal<boolean>(false);
  readonly overrideEndpointId = signal<string | null>(null);
  readonly copiedField = signal<string | null>(null);

  constructor() {
    effect(() => {
      const open = this.isOpen();
      const id = this.deliveryId();
      if (open && id) {
        this.loadDelivery(id);
      }
    });
  }

  readonly currentDeliveryId = computed(() => {
    return this.deliveryId() || this.realtimeEvent()?.deliveryId || null;
  });

  readonly displayEventType = computed(() => {
    return this.deliveryDetail()?.eventType || this.realtimeEvent()?.eventName || 'Webhook Delivery';
  });

  readonly currentStatus = computed(() => {
    return this.deliveryDetail()?.status || this.realtimeEvent()?.status || null;
  });

  readonly targetEndpointUrl = computed(() => {
    const detail = this.deliveryDetail();
    if (detail?.endpointUrl) return detail.endpointUrl;

    const epId = detail?.endpointId || this.realtimeEvent()?.endpointId;
    if (epId) {
      const match = this.endpoints().find(e => e.id === epId);
      if (match) return match.targetUrl;
    }

    return this.realtimeEvent()?.endpointUrl || null;
  });

  readonly correlationId = computed(() => {
    return this.deliveryDetail()?.correlationId || this.realtimeEvent()?.correlationId || 'N/A';
  });

  readonly originalDeliveryId = computed(() => {
    return this.deliveryDetail()?.originalDeliveryId || this.realtimeEvent()?.originalDeliveryId || null;
  });

  readonly traceParent = computed(() => {
    return this.deliveryDetail()?.traceParent || this.realtimeEvent()?.traceParent || null;
  });

  readonly attempts = computed<DeliveryAttempt[]>(() => {
    const detail = this.deliveryDetail();
    if (detail && detail.attempts && detail.attempts.length > 0) {
      return detail.attempts;
    }

    const rt = this.realtimeEvent();
    if (rt?.attempt) {
      return [{
        id: rt.attempt.id,
        deliveryId: rt.deliveryId,
        attemptNumber: rt.attempt.attemptNumber,
        httpStatusCode: rt.attempt.httpStatusCode,
        requestHeadersJson: rt.attempt.requestHeadersJson,
        requestBody: rt.attempt.requestBody,
        responseHeadersJson: rt.attempt.responseHeadersJson,
        responseBody: rt.attempt.responseBody,
        elapsedMs: rt.attempt.elapsedMs,
        errorMessage: rt.attempt.errorMessage,
        executedAt: rt.attempt.executedAt
      }];
    }

    return [];
  });

  readonly currentAttempt = computed<DeliveryAttempt | null>(() => {
    const list = this.attempts();
    if (list.length === 0) return null;
    const idx = this.selectedAttemptIndex();
    return list[idx] || list[list.length - 1];
  });

  readonly signatureHeader = computed<string | null>(() => {
    const attempt = this.currentAttempt();
    if (!attempt?.requestHeadersJson) return null;
    try {
      const headers = JSON.parse(attempt.requestHeadersJson);
      for (const key of Object.keys(headers)) {
        if (key.toLowerCase() === 'x-hookbridge-signature') {
          return headers[key];
        }
      }
    } catch {
      // Ignored
    }
    return null;
  });

  readonly payloadByteSize = computed<number>(() => {
    const body = this.currentAttempt()?.requestBody;
    if (!body) return 0;
    return new TextEncoder().encode(body).length;
  });

  readonly parsedTrace = computed(() => {
    const raw = this.traceParent();
    if (!raw) return { traceId: '', spanId: '', flags: '' };
    const parts = raw.split('-');
    if (parts.length >= 4) {
      return {
        traceId: parts[1],
        spanId: parts[2],
        flags: parts[3]
      };
    }
    return { traceId: raw, spanId: '', flags: '' };
  });

  readonly generatedCurlCommand = computed<string>(() => {
    const url = this.targetEndpointUrl() || 'https://api.example.com/webhook';
    const attempt = this.currentAttempt();
    const headersJson = attempt?.requestHeadersJson;
    const body = attempt?.requestBody || '{}';

    let headerFlags = `-H "Content-Type: application/json"`;
    if (headersJson) {
      try {
        const headers = JSON.parse(headersJson);
        const parts: string[] = [];
        for (const [k, v] of Object.entries(headers)) {
          parts.push(`-H "${k}: ${v}"`);
        }
        if (parts.length > 0) {
          headerFlags = parts.join(' \\\n  ');
        }
      } catch {
        // Fallback
      }
    }

    const escapedBody = body.replace(/'/g, `'\\''`);
    return `curl -X POST "${url}" \\\n  ${headerFlags} \\\n  -d '${escapedBody}'`;
  });

  loadDelivery(id: string): void {
    this.isLoading.set(true);
    this.errorState.set(null);

    this.deliveryService.getDeliveryById(id).subscribe({
      next: (detail) => {
        this.deliveryDetail.set(detail);
        this.selectedAttemptIndex.set(Math.max(0, (detail.attempts?.length || 1) - 1));
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load delivery details:', err);
        this.errorState.set(err.error?.detail || 'Failed to retrieve delivery record.');
        this.isLoading.set(false);
      }
    });
  }

  retryLoad(): void {
    const id = this.currentDeliveryId();
    if (id) this.loadDelivery(id);
  }

  selectAttempt(index: number): void {
    this.selectedAttemptIndex.set(index);
  }

  getAttemptDotClasses(att: DeliveryAttempt): string {
    if (att.httpStatusCode >= 200 && att.httpStatusCode < 300) {
      return 'bg-emerald-950 border-emerald-500 text-emerald-400';
    }
    if (att.httpStatusCode >= 400 || att.errorMessage) {
      return 'bg-rose-950 border-rose-500 text-rose-400';
    }
    return 'bg-surface-800 border-surface-600 text-surface-300';
  }

  getHttpStatusClasses(code?: number | null): string {
    if (!code) return 'bg-surface-800 text-surface-400 border border-surface-700';
    if (code >= 200 && code < 300) return 'bg-emerald-500/15 text-emerald-300 border border-emerald-500/30';
    if (code >= 300 && code < 400) return 'bg-sky-500/15 text-sky-300 border border-sky-500/30';
    if (code >= 400 && code < 500) return 'bg-amber-500/15 text-amber-300 border border-amber-500/30';
    return 'bg-rose-500/15 text-rose-300 border border-rose-500/30';
  }

  async copyToClipboard(text: string, field: string): Promise<void> {
    if (!text) return;
    try {
      await navigator.clipboard.writeText(text);
      this.copiedField.set(field);
      setTimeout(() => this.copiedField.set(null), 2000);
    } catch {
      // Ignored
    }
  }

  triggerReplay(overrideId?: string | null): void {
    const id = this.currentDeliveryId();
    if (!id) return;

    this.isReplaying.set(true);
    this.deliveryService.replayDelivery(id, overrideId || undefined).subscribe({
      next: () => {
        this.isReplaying.set(false);
        this.closeOverrideModal();
        this.toast.success('Webhook delivery re-enqueued for transmission', 'Replay Dispatched');
        this.replayed.emit();
        this.loadDelivery(id);
      },
      error: (err) => {
        this.isReplaying.set(false);
        const msg = err.error?.detail || 'Failed to trigger delivery replay.';
        this.toast.error(msg, 'Replay Failed');
      }
    });
  }

  openOverrideModal(): void {
    this.overrideEndpointId.set(null);
    this.showOverrideModal.set(true);
  }

  closeOverrideModal(): void {
    this.showOverrideModal.set(false);
  }

  onClose(): void {
    this.closed.emit();
  }
}
