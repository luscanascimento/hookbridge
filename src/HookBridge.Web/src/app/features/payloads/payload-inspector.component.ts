import { Component, OnInit, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { PayloadService, SamplePayloadPreset } from '../../core/services/payload.service';
import { DeliveryService } from '../../core/services/delivery.service';
import { ToastService } from '../../shared/components/ui/toast/toast.service';
import { TabGroupComponent } from '../../shared/components/ui/tab-group.component';
import { TabComponent } from '../../shared/components/ui/tab.component';
import { JsonTreeViewerComponent } from '../../shared/components/ui/json-tree-viewer.component';
import { PayloadDiffViewerComponent } from '../../shared/components/ui/payload-diff-viewer.component';
import { JsonPathEvaluatorComponent } from '../../shared/components/ui/json-path-evaluator.component';
import { PayloadAnalyzerComponent } from '../../shared/components/ui/payload-analyzer.component';
import { CodeViewerComponent } from '../../shared/components/ui/code-viewer.component';

@Component({
  selector: 'app-payload-inspector',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    TabGroupComponent,
    TabComponent,
    JsonTreeViewerComponent,
    PayloadDiffViewerComponent,
    JsonPathEvaluatorComponent,
    PayloadAnalyzerComponent
  ],
  template: `
    <div class="space-y-6 max-w-7xl mx-auto pb-12">
      <!-- Page Header -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2.5">
            <span class="p-2 rounded-xl bg-brand-600/20 border border-brand-500/30 text-brand-400">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4"/>
              </svg>
            </span>
            Payload Inspector & Query Lab
          </h1>
          <p class="text-xs text-surface-400 mt-1">
            Deep-dive inspection, interactive JSON tree exploration, JSONPath query evaluation, and schema validation.
          </p>
        </div>

        <!-- Sample Presets Dropdown & Delivery Loader -->
        <div class="flex items-center gap-2">
          <div class="relative">
            <select
              (change)="onPresetSelected($event)"
              class="bg-surface-900 border border-surface-700 rounded-lg px-3 py-1.5 text-surface-200 text-xs font-sans focus:border-brand-500 focus:outline-none">
              <option value="">-- Load Sample Webhook --</option>
              @for (preset of presets; track preset.id) {
                <option [value]="preset.id">{{ preset.name }}</option>
              }
            </select>
          </div>
        </div>
      </div>

      <!-- Main Dual-Pane Layout -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        
        <!-- Left Pane: Raw Payload Editor & Tools -->
        <div class="lg:col-span-5 space-y-4">
          <div class="rounded-xl border border-surface-800 bg-surface-900/60 p-4 space-y-3">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <span class="text-xs font-semibold text-white uppercase tracking-wider font-sans">Raw JSON Payload</span>
                <span class="text-[10px] font-mono px-2 py-0.5 rounded bg-surface-800 text-surface-400 border border-surface-700">
                  {{ rawByteCount() }} bytes
                </span>
              </div>

              <!-- Quick formatting actions -->
              <div class="flex items-center gap-1.5">
                <button
                  type="button"
                  (click)="prettifyJson()"
                  title="Format JSON"
                  class="px-2 py-1 bg-surface-800 hover:bg-surface-700 text-surface-200 hover:text-white rounded text-[11px] font-sans transition-colors">
                  Prettify
                </button>
                <button
                  type="button"
                  (click)="minifyJson()"
                  title="Minify JSON"
                  class="px-2 py-1 bg-surface-800 hover:bg-surface-700 text-surface-200 hover:text-white rounded text-[11px] font-sans transition-colors">
                  Minify
                </button>
                <button
                  type="button"
                  (click)="clearPayload()"
                  title="Clear"
                  class="px-2 py-1 bg-surface-800 hover:bg-rose-900/50 text-surface-400 hover:text-rose-300 rounded text-[11px] font-sans transition-colors">
                  Clear
                </button>
              </div>
            </div>

            <!-- JSON Input Area -->
            <div class="relative">
              <textarea
                [(ngModel)]="rawPayload"
                (ngModelChange)="onPayloadChanged()"
                rows="22"
                placeholder="Paste or edit webhook JSON payload here..."
                class="w-full bg-surface-950 border border-surface-800 rounded-xl p-3.5 text-surface-100 font-mono text-xs focus:border-brand-500 focus:outline-none placeholder:text-surface-600 shadow-inner resize-y transition-colors"
                [ngClass]="{'border-rose-500/50': !isJsonValid()}">
              </textarea>

              @if (!isJsonValid()) {
                <div class="mt-1.5 p-2 bg-rose-950/40 border border-rose-800/60 rounded-lg text-rose-300 text-[11px] font-mono flex items-center gap-1.5">
                  <svg class="w-3.5 h-3.5 text-rose-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
                  </svg>
                  <span>Malformed JSON syntax</span>
                </div>
              }
            </div>

            <!-- Load from Delivery ID Tool -->
            <div class="pt-2 border-t border-surface-800/80 space-y-2">
              <label class="block text-[11px] font-semibold text-surface-400 uppercase tracking-wider">
                Load from Delivery Record
              </label>
              <div class="flex items-center gap-2">
                <input
                  type="text"
                  [(ngModel)]="deliveryIdInput"
                  placeholder="Paste Delivery ID (UUID)..."
                  class="flex-1 bg-surface-950 border border-surface-700 rounded-lg px-3 py-1.5 text-white font-mono text-xs focus:border-brand-500 focus:outline-none placeholder:text-surface-600" />
                <button
                  type="button"
                  (click)="loadFromDeliveryId()"
                  [disabled]="isLoadingDelivery() || !deliveryIdInput()"
                  class="px-3 py-1.5 bg-surface-800 hover:bg-surface-700 disabled:opacity-50 text-surface-200 text-xs rounded-lg transition-colors font-sans">
                  {{ isLoadingDelivery() ? 'Loading...' : 'Fetch' }}
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- Right Pane: Interactive Tools & Analytics Tabs -->
        <div class="lg:col-span-7 space-y-4">
          <app-tab-group [(activeTab)]="activeTab">
            
            <!-- Tab 1: Interactive JSON Tree -->
            <app-tab id="tree" label="Interactive Tree" badge="Tree">
              <div class="pt-2 space-y-4">
                <app-json-tree-viewer
                  [json]="rawPayload()"
                  [highlightPaths]="highlightedPaths()">
                </app-json-tree-viewer>
              </div>
            </app-tab>

            <!-- Tab 2: JSONPath Query Lab -->
            <app-tab id="jsonpath" label="JSONPath Query Lab" badge="Query">
              <div class="pt-2 space-y-4">
                <app-json-path-evaluator
                  [payloadJson]="rawPayload()"
                  (matchesFound)="onMatchesFound($event)">
                </app-json-path-evaluator>
              </div>
            </app-tab>

            <!-- Tab 3: Diff & Compare Engine -->
            <app-tab id="diff" label="Payload Diff & Compare" badge="Diff">
              <div class="pt-2 space-y-4">
                <!-- Secondary Payload Input for Comparison -->
                <div class="p-3 bg-surface-900/60 rounded-xl border border-surface-800 space-y-2">
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-semibold text-surface-300">Comparison Target (Right JSON)</span>
                    <button
                      type="button"
                      (click)="loadModifiedDiffPreset()"
                      class="text-[11px] text-brand-400 hover:text-brand-300 font-sans transition-colors">
                      Load Simulated Mutation
                    </button>
                  </div>
                  <textarea
                    [(ngModel)]="comparisonPayload"
                    rows="6"
                    placeholder="Paste second JSON payload to compare against..."
                    class="w-full bg-surface-950 border border-surface-700 rounded-lg p-2.5 text-white font-mono text-xs focus:border-brand-500 focus:outline-none placeholder:text-surface-600 shadow-inner resize-y">
                  </textarea>
                </div>

                <app-payload-diff-viewer
                  [leftJson]="rawPayload()"
                  [rightJson]="comparisonPayload()">
                </app-payload-diff-viewer>
              </div>
            </app-tab>

            <!-- Tab 4: Size & Structure Analytics -->
            <app-tab id="analytics" label="Size & Structure" badge="Stats">
              <div class="pt-2">
                <app-payload-analyzer
                  [payloadJson]="rawPayload()">
                </app-payload-analyzer>
              </div>
            </app-tab>

          </app-tab-group>
        </div>

      </div>
    </div>
  `
})
export class PayloadInspectorComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly payloadService = inject(PayloadService);
  private readonly deliveryService = inject(DeliveryService);
  private readonly toast = inject(ToastService);

  readonly presets: SamplePayloadPreset[] = this.payloadService.getSamplePresets();

  readonly rawPayload = signal<string>('');
  readonly comparisonPayload = signal<string>('');
  readonly activeTab = signal<string>('tree');
  readonly deliveryIdInput = signal<string>('');
  readonly isLoadingDelivery = signal<boolean>(false);
  readonly highlightedPaths = signal<string[]>([]);

  ngOnInit(): void {
    // Check query params
    this.route.queryParams.subscribe(params => {
      if (params['deliveryId']) {
        this.deliveryIdInput.set(params['deliveryId']);
        this.loadFromDeliveryId();
      } else if (params['json']) {
        this.rawPayload.set(params['json']);
      } else if (this.presets.length > 0) {
        // Default to first preset
        this.rawPayload.set(this.presets[0].json);
        this.setupDefaultComparison(this.presets[0].json);
      }
    });
  }

  readonly rawByteCount = computed(() => {
    return this.payloadService.calculateByteSize(this.rawPayload());
  });

  readonly isJsonValid = computed(() => {
    const raw = this.rawPayload().trim();
    if (!raw) return true;
    try {
      JSON.parse(raw);
      return true;
    } catch {
      return false;
    }
  });

  onPayloadChanged(): void {
    // Clear path highlights if payload drastically changes
    this.highlightedPaths.set([]);
  }

  onPresetSelected(event: Event): void {
    const select = event.target as HTMLSelectElement;
    const presetId = select.value;
    if (!presetId) return;

    const match = this.presets.find(p => p.id === presetId);
    if (match) {
      this.rawPayload.set(match.json);
      this.setupDefaultComparison(match.json);
      this.toast.info(`Loaded sample webhook: ${match.name}`, 'Preset Loaded');
    }
  }

  private setupDefaultComparison(originalJson: string): void {
    try {
      const parsed = JSON.parse(originalJson);
      // Simulate modified payload
      parsed._modified_at = new Date().toISOString();
      if (parsed.status) parsed.status = 'processed';
      if (parsed.data?.object?.status) parsed.data.object.status = 'refunded';
      if (parsed.line_items) parsed.line_items.push({ id: 999999, title: 'Warranty Protection (2 Yr)', price: '19.00', quantity: 1 });
      this.comparisonPayload.set(JSON.stringify(parsed, null, 2));
    } catch {
      this.comparisonPayload.set(originalJson);
    }
  }

  loadModifiedDiffPreset(): void {
    this.setupDefaultComparison(this.rawPayload());
    this.toast.info('Simulated mutated payload loaded in comparison target', 'Mutation Ready');
  }

  prettifyJson(): void {
    const formatted = this.payloadService.formatJson(this.rawPayload());
    this.rawPayload.set(formatted);
  }

  minifyJson(): void {
    const minified = this.payloadService.minifyJson(this.rawPayload());
    this.rawPayload.set(minified);
  }

  clearPayload(): void {
    this.rawPayload.set('{}');
  }

  loadFromDeliveryId(): void {
    const id = this.deliveryIdInput().trim();
    if (!id) return;

    this.isLoadingDelivery.set(true);
    this.deliveryService.getDeliveryById(id).subscribe({
      next: (detail) => {
        this.isLoadingDelivery.set(false);
        const lastAttempt = detail.attempts && detail.attempts.length > 0
          ? detail.attempts[detail.attempts.length - 1]
          : null;

        const body = lastAttempt?.requestBody || '{}';
        this.rawPayload.set(this.payloadService.formatJson(body));
        if (detail.attempts && detail.attempts.length > 1) {
          const firstAttempt = detail.attempts[0];
          this.comparisonPayload.set(this.payloadService.formatJson(firstAttempt.requestBody || '{}'));
        } else {
          this.setupDefaultComparison(body);
        }
        this.toast.success(`Loaded payload from delivery ${id.slice(0, 8)}...`, 'Delivery Loaded');
      },
      error: (err) => {
        this.isLoadingDelivery.set(false);
        this.toast.error(err.error?.detail || 'Failed to fetch delivery record.', 'Load Error');
      }
    });
  }

  onMatchesFound(paths: string[]): void {
    this.highlightedPaths.set(paths);
  }
}
