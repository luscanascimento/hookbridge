import { Component, OnInit, inject, signal, computed, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  ButtonComponent,
  StatusBadgeComponent,
  SkeletonLoaderComponent,
  EmptyStateComponent,
  SlideOverComponent,
  CodeViewerComponent,
  TabGroupComponent,
  TabComponent,
  MetricCardComponent
} from '../../shared/components';
import { EventSchemaService } from '../../core/services/event-schema.service';
import { ToastService } from '../../shared/components/ui/toast/toast.service';
import {
  EventSchemaSummary,
  EventSchemaDetail,
  EventSchemaVersion,
  SchemaCompatibilityMode,
  SchemaStatus,
  DetectSchemaDriftResponse,
  SchemaDocumentationResponse,
  ValidateEventPayloadResponse
} from '../../core/models/event-schema.models';
import { SchemaCreateModalComponent } from './schema-create-modal.component';
import { SchemaVersionModalComponent } from './schema-version-modal.component';

@Component({
  selector: 'app-schemas',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonComponent,
    StatusBadgeComponent,
    SkeletonLoaderComponent,
    EmptyStateComponent,
    SlideOverComponent,
    CodeViewerComponent,
    TabGroupComponent,
    TabComponent,
    MetricCardComponent,
    SchemaCreateModalComponent,
    SchemaVersionModalComponent
  ],
  template: `
    <div class="space-y-6">
      <!-- Page Header -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-xl font-bold tracking-tight text-white flex items-center gap-2">
            Event Schema Registry
            <span class="text-xs font-mono font-normal bg-brand-500/10 text-brand-300 border border-brand-500/20 px-2 py-0.5 rounded">
              Draft 2020-12
            </span>
          </h1>
          <p class="text-xs text-surface-400 mt-1">
            Manage event contracts, enforce evolution compatibility policies, detect payload drift, and export SDK typings.
          </p>
        </div>

        <div class="flex items-center gap-2">
          <app-button variant="outline" size="sm" (click)="loadSchemas()" [disabled]="isLoading()">
            <svg class="w-4 h-4 mr-1.5" [class.animate-spin]="isLoading()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
            </svg>
            Refresh
          </app-button>
          <app-button variant="primary" size="sm" (click)="openCreateModal()">
            <svg class="w-4 h-4 mr-1.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
            </svg>
            Register Schema
          </app-button>
        </div>
      </div>

      <!-- Metric KPI Cards -->
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <app-metric-card
          label="Total Event Schemas"
          [value]="totalSchemasCount()"
          period="Cataloged">
        </app-metric-card>

        <app-metric-card
          label="Active Schemas"
          [value]="activeSchemasCount()"
          period="Operational">
        </app-metric-card>

        <app-metric-card
          label="Total Versions"
          [value]="totalVersionsCount()"
          period="Evolutions">
        </app-metric-card>

        <app-metric-card
          label="Strict Policies"
          [value]="strictPoliciesCount()"
          period="Enforced">
        </app-metric-card>
      </div>

      <!-- Search & Filters -->
      <div class="flex flex-col md:flex-row gap-3 items-center justify-between">
        <div class="w-full md:w-80">
          <input type="text" [(ngModel)]="searchQuery" (ngModelChange)="filterSchemas()"
                 placeholder="Search by event type or name..."
                 class="w-full bg-surface-900 border border-surface-800 rounded-lg px-3 py-2 text-xs text-surface-200 focus:outline-none focus:border-brand-500" />
        </div>

        <div class="flex items-center gap-2 overflow-x-auto w-full md:w-auto pb-1">
          <!-- Compatibility filter chips -->
          <div class="flex items-center bg-surface-900 border border-surface-800 rounded-lg p-0.5">
            @for (mode of compatFilterOptions; track mode) {
              <button (click)="setCompatFilter(mode)"
                      class="px-2.5 py-1 text-xs rounded transition-colors"
                      [class.bg-brand-500]="selectedCompatFilter() === mode"
                      [class.text-white]="selectedCompatFilter() === mode"
                      [class.text-surface-400]="selectedCompatFilter() !== mode">
                {{ mode }}
              </button>
            }
          </div>

          <!-- Status filter chips -->
          <div class="flex items-center bg-surface-900 border border-surface-800 rounded-lg p-0.5">
            @for (st of statusFilterOptions; track st) {
              <button (click)="setStatusFilter(st)"
                      class="px-2.5 py-1 text-xs rounded transition-colors"
                      [class.bg-brand-500]="selectedStatusFilter() === st"
                      [class.text-white]="selectedStatusFilter() === st"
                      [class.text-surface-400]="selectedStatusFilter() !== st">
                {{ st }}
              </button>
            }
          </div>
        </div>
      </div>

      <!-- Schemas List -->
      @if (isLoading()) {
        <div class="space-y-3">
          <app-skeleton-loader variant="card" height="4rem"></app-skeleton-loader>
          <app-skeleton-loader variant="card" height="4rem"></app-skeleton-loader>
          <app-skeleton-loader variant="card" height="4rem"></app-skeleton-loader>
        </div>
      } @else if (filteredSchemas().length === 0) {
        <app-empty-state
          title="No Event Schemas Found"
          description="Register your first JSON Schema contract to enforce webhook payload structure and prevent breaking changes."
          actionText="Register Schema"
          (action)="openCreateModal()">
        </app-empty-state>
      } @else {
        <div class="bg-surface-900 border border-surface-800 rounded-xl overflow-hidden divide-y divide-surface-800">
          @for (s of filteredSchemas(); track s.id) {
            <div class="p-4 hover:bg-surface-850/60 transition-colors flex flex-col md:flex-row md:items-center justify-between gap-4 cursor-pointer"
                 (click)="inspectSchema(s.id)">
              <div class="flex items-start gap-3 min-w-0">
                <div class="w-9 h-9 rounded-lg bg-surface-800 border border-surface-700 flex items-center justify-center shrink-0 font-mono text-brand-400 font-bold text-xs">
                  JS
                </div>
                <div class="min-w-0">
                  <div class="flex items-center gap-2 flex-wrap">
                    <span class="font-mono text-xs font-semibold text-white tracking-tight">{{ s.eventType }}</span>
                    <span class="text-xs text-surface-400">• {{ s.name }}</span>
                    @if (s.activeVersion) {
                      <span class="text-[11px] font-mono px-2 py-0.2 rounded bg-brand-500/10 text-brand-300 border border-brand-500/30">
                        v{{ s.activeVersion }}
                      </span>
                    }
                  </div>
                  <p class="text-xs text-surface-400 truncate mt-0.5">{{ s.description || 'No description provided.' }}</p>
                </div>
              </div>

              <div class="flex items-center gap-3 shrink-0" (click)="$event.stopPropagation()">
                <!-- Compatibility Mode Badge -->
                <span class="text-[11px] font-mono px-2 py-0.5 rounded border"
                      [class.bg-emerald-950-40]="s.compatibilityMode === 'Full'"
                      [class.border-emerald-800-60]="s.compatibilityMode === 'Full'"
                      [class.text-emerald-400]="s.compatibilityMode === 'Full'"
                      [class.bg-blue-950-40]="s.compatibilityMode === 'Backward'"
                      [class.border-blue-800-60]="s.compatibilityMode === 'Backward'"
                      [class.text-blue-400]="s.compatibilityMode === 'Backward'"
                      [class.bg-amber-950-40]="s.compatibilityMode === 'Forward'"
                      [class.border-amber-800-60]="s.compatibilityMode === 'Forward'"
                      [class.text-amber-400]="s.compatibilityMode === 'Forward'"
                      [class.bg-surface-800]="s.compatibilityMode === 'None'"
                      [class.border-surface-700]="s.compatibilityMode === 'None'"
                      [class.text-surface-400]="s.compatibilityMode === 'None'">
                  {{ s.compatibilityMode }}
                </span>

                <span class="text-xs text-surface-400 font-mono">
                  {{ s.totalVersions }} {{ s.totalVersions === 1 ? 'version' : 'versions' }}
                </span>

                <app-status-badge [status]="s.status" size="sm"></app-status-badge>

                <div class="flex items-center gap-1">
                  <button (click)="inspectSchema(s.id)" title="Inspect Schema"
                          class="p-1.5 text-surface-400 hover:text-brand-300 hover:bg-surface-800 rounded transition-colors">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"/>
                    </svg>
                  </button>

                  <button (click)="openDriftAuditDirect(s)" title="Audit Contract Drift"
                          class="p-1.5 text-surface-400 hover:text-amber-300 hover:bg-amber-950/30 rounded transition-colors">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/>
                    </svg>
                  </button>

                  <button (click)="deleteSchema(s)" title="Delete Schema"
                          class="p-1.5 text-surface-400 hover:text-rose-400 hover:bg-rose-950/30 rounded transition-colors">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/>
                    </svg>
                  </button>
                </div>
              </div>
            </div>
          }
        </div>
      }

      <!-- Slide-Over Drawer: Schema Details & Evolution Studio -->
      <app-slide-over [isOpen]="isDrawerOpen()" (closed)="closeDrawer()" [title]="drawerTitle()" size="xl">
        @if (selectedSchema()) {
          <div class="space-y-6">
            <!-- Header Summary Bar -->
            <div class="bg-surface-900 border border-surface-800 rounded-xl p-4 flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div>
                <div class="flex items-center gap-2">
                  <span class="font-mono text-sm font-bold text-white">{{ selectedSchema()!.eventType }}</span>
                  <span class="text-xs font-mono px-2 py-0.5 rounded bg-brand-500/10 text-brand-300 border border-brand-500/30">
                    Active: v{{ selectedSchema()!.activeVersion?.version || 'None' }}
                  </span>
                </div>
                <p class="text-xs text-surface-400 mt-0.5">{{ selectedSchema()!.name }} — {{ selectedSchema()!.description || 'No description' }}</p>
              </div>

              <div class="flex items-center gap-2 shrink-0">
                <app-button variant="primary" size="sm" (click)="openVersionModal()">
                  <svg class="w-3.5 h-3.5 mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
                  </svg>
                  New Version
                </app-button>
              </div>
            </div>

            <!-- Drawer Tabs -->
            <app-tab-group [(activeTab)]="activeTabId">
              <!-- TAB 1: Evolution & Versions -->
              <app-tab id="versions" label="Versions & Evolution">
                <div class="space-y-4 pt-3">
                  <div class="space-y-3">
                    @for (ver of selectedSchema()!.versions; track ver.id) {
                      <div class="bg-surface-900 border rounded-lg p-3.5 space-y-3 transition-colors"
                           [class.border-brand-500-40]="ver.isActive"
                           [class.border-surface-800]="!ver.isActive">
                        <div class="flex items-center justify-between flex-wrap gap-2">
                          <div class="flex items-center gap-2">
                            <span class="font-mono text-xs font-bold text-white">v{{ ver.version }}</span>
                            <span class="text-[11px] font-mono text-surface-400">#{{ ver.versionNumber }}</span>
                            @if (ver.isActive) {
                              <span class="text-[10px] font-mono bg-emerald-950/60 text-emerald-400 border border-emerald-800/60 px-2 py-0.2 rounded font-semibold">
                                ACTIVE
                              </span>
                            }
                            @if (ver.isDeprecated) {
                              <span class="text-[10px] font-mono bg-rose-950/60 text-rose-400 border border-rose-800/60 px-2 py-0.2 rounded">
                                DEPRECATED
                              </span>
                            }
                          </div>

                          <div class="flex items-center gap-2 text-xs">
                            <span class="text-surface-500 text-[11px]">{{ ver.createdAt | date:'short' }}</span>
                            @if (!ver.isActive) {
                              <button (click)="activateVersion(ver.id)"
                                      class="px-2 py-0.5 rounded bg-surface-800 text-brand-300 hover:bg-surface-700 border border-surface-700 text-[11px]">
                                Activate
                              </button>
                            }
                            @if (!ver.isDeprecated) {
                              <button (click)="deprecateVersion(ver.id)"
                                      class="px-2 py-0.5 rounded bg-surface-800 text-rose-300 hover:bg-surface-700 border border-surface-700 text-[11px]">
                                Deprecate
                              </button>
                            }
                          </div>
                        </div>

                        <p class="text-xs text-surface-300">{{ ver.description || 'No release notes provided.' }}</p>

                        <!-- Expandable / Raw Schema Code -->
                        <app-code-viewer [code]="ver.schemaJson" language="json"></app-code-viewer>
                      </div>
                    }
                  </div>
                </div>
              </app-tab>

              <!-- TAB 2: Documentation & SDK Types -->
              <app-tab id="docs" label="Interactive Docs & SDK">
                <div class="space-y-4 pt-3">
                  @if (isLoadingDocs()) {
                    <app-skeleton-loader variant="card" height="10rem"></app-skeleton-loader>
                  } @else if (schemaDocs()) {
                    <!-- TypeScript Definition -->
                    <div class="space-y-1.5">
                      <div class="flex items-center justify-between">
                        <span class="text-xs font-semibold text-surface-300">TypeScript Interface</span>
                        <button (click)="copyToClipboard(schemaDocs()!.typeScriptSnippet, 'TypeScript')"
                                class="text-xs text-brand-400 hover:text-brand-300">
                          Copy TS
                        </button>
                      </div>
                      <app-code-viewer [code]="schemaDocs()!.typeScriptSnippet" language="typescript"></app-code-viewer>
                    </div>

                    <!-- C# Definition -->
                    <div class="space-y-1.5">
                      <div class="flex items-center justify-between">
                        <span class="text-xs font-semibold text-surface-300">C# Record Model</span>
                        <button (click)="copyToClipboard(schemaDocs()!.cSharpSnippet, 'C# Record')"
                                class="text-xs text-brand-400 hover:text-brand-300">
                          Copy C#
                        </button>
                      </div>
                      <app-code-viewer [code]="schemaDocs()!.cSharpSnippet" language="csharp"></app-code-viewer>
                    </div>

                    <!-- Example Payload -->
                    <div class="space-y-1.5">
                      <div class="flex items-center justify-between">
                        <span class="text-xs font-semibold text-surface-300">Generated Example Payload JSON</span>
                        <button (click)="copyToClipboard(schemaDocs()!.samplePayloadJson, 'Sample Payload')"
                                class="text-xs text-brand-400 hover:text-brand-300">
                          Copy JSON
                        </button>
                      </div>
                      <app-code-viewer [code]="schemaDocs()!.samplePayloadJson" language="json"></app-code-viewer>
                    </div>
                  }
                </div>
              </app-tab>

              <!-- TAB 3: Contract Drift Detector -->
              <app-tab id="drift" label="Contract Drift Detector">
                <div class="space-y-4 pt-3">
                  <div class="bg-surface-900 border border-surface-800 rounded-lg p-4 space-y-3">
                    <div class="flex items-center justify-between">
                      <div>
                        <h3 class="text-xs font-bold text-white uppercase tracking-wider">Payload Drift Audit</h3>
                        <p class="text-xs text-surface-400">Validates recent deliveries against the active schema contract.</p>
                      </div>
                      <app-button variant="outline" size="sm" [loading]="isRunningDriftAudit()" (click)="runDriftAudit()">
                        Run Audit
                      </app-button>
                    </div>

                    @if (driftReport()) {
                      <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 pt-2">
                        <div class="bg-surface-950 p-2.5 rounded border border-surface-800">
                          <span class="text-[11px] text-surface-400">Analyzed</span>
                          <div class="text-base font-bold font-mono text-white">{{ driftReport()!.deliveriesAnalyzed }}</div>
                        </div>
                        <div class="bg-surface-950 p-2.5 rounded border border-surface-800">
                          <span class="text-[11px] text-surface-400">Conforming</span>
                          <div class="text-base font-bold font-mono text-emerald-400">{{ driftReport()!.conformingDeliveries }}</div>
                        </div>
                        <div class="bg-surface-950 p-2.5 rounded border border-surface-800">
                          <span class="text-[11px] text-surface-400">Drift Violations</span>
                          <div class="text-base font-bold font-mono text-rose-400">{{ driftReport()!.nonConformingDeliveries }}</div>
                        </div>
                        <div class="bg-surface-950 p-2.5 rounded border border-surface-800">
                          <span class="text-[11px] text-surface-400">Conformance</span>
                          <div class="text-base font-bold font-mono"
                               [class.text-emerald-400]="driftReport()!.conformanceRatePercentage >= 95"
                               [class.text-amber-400]="driftReport()!.conformanceRatePercentage >= 80 && driftReport()!.conformanceRatePercentage < 95"
                               [class.text-rose-400]="driftReport()!.conformanceRatePercentage < 80">
                            {{ driftReport()!.conformanceRatePercentage }}%
                          </div>
                        </div>
                      </div>

                      @if (driftReport()!.detectedDrifts.length > 0) {
                        <div class="space-y-2 pt-2">
                          <span class="text-xs font-semibold text-surface-300">Detected Contract Anomalies</span>
                          <div class="space-y-1.5 max-h-60 overflow-y-auto">
                            @for (item of driftReport()!.detectedDrifts; track item.path + item.reason) {
                              <div class="bg-surface-950 border border-surface-800 rounded p-2 text-xs flex items-start justify-between gap-2">
                                <div class="min-w-0">
                                  <div class="flex items-center gap-1.5">
                                    <span class="font-mono text-brand-300 text-[11px]">{{ item.path }}</span>
                                    <span class="text-[10px] font-mono px-1.5 py-0.2 rounded"
                                          [class.bg-rose-950-60]="item.severity === 'Error'"
                                          [class.text-rose-400]="item.severity === 'Error'"
                                          [class.bg-amber-950-60]="item.severity === 'Warning'"
                                          [class.text-amber-400]="item.severity === 'Warning'">
                                      {{ item.severity }}
                                    </span>
                                  </div>
                                  <p class="text-surface-400 text-[11px] mt-0.5">{{ item.reason }}</p>
                                </div>
                                @if (item.sampleValue) {
                                  <span class="font-mono text-[10px] text-surface-500 truncate max-w-[120px] shrink-0">
                                    {{ item.sampleValue }}
                                  </span>
                                }
                              </div>
                            }
                          </div>
                        </div>
                      } @else {
                        <div class="text-xs text-emerald-400 bg-emerald-950/30 border border-emerald-800/40 rounded p-3 text-center">
                          ✓ All analyzed deliveries strictly conform to the active schema contract.
                        </div>
                      }
                    }
                  </div>
                </div>
              </app-tab>

              <!-- TAB 4: Payload Validation Sandbox -->
              <app-tab id="sandbox" label="Validation Sandbox">
                <div class="space-y-4 pt-3">
                  <div>
                    <div class="flex items-center justify-between mb-1.5">
                      <label class="text-xs font-medium text-surface-300">Test Payload JSON</label>
                      <button type="button" (click)="loadSamplePayloadIntoSandbox()"
                              class="text-xs text-brand-400 hover:text-brand-300">
                        Load Sample Payload
                      </button>
                    </div>
                    <textarea [(ngModel)]="sandboxPayload" rows="7"
                              placeholder="{ ... JSON payload to test ... }"
                              class="w-full bg-surface-950 border border-surface-700 rounded-lg p-3 text-xs font-mono text-surface-200 focus:outline-none focus:border-brand-500 leading-relaxed"></textarea>
                  </div>

                  <div class="flex items-center justify-end">
                    <app-button variant="primary" size="sm" [loading]="isValidatingSandbox()" (click)="validateSandboxPayload()">
                      Validate Against Schema
                    </app-button>
                  </div>

                  @if (sandboxValidationResult()) {
                    @if (sandboxValidationResult()!.isValid) {
                      <div class="bg-emerald-950/40 border border-emerald-800/60 rounded-lg p-3 text-xs text-emerald-300 flex items-center gap-2">
                        <svg class="w-4 h-4 text-emerald-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/>
                        </svg>
                        <span>Valid! Payload matches <strong>{{ sandboxValidationResult()!.eventType }}</strong> (v{{ sandboxValidationResult()!.version }}).</span>
                      </div>
                    } @else {
                      <div class="bg-rose-950/40 border border-rose-800/60 rounded-lg p-3 text-xs text-rose-300 space-y-1.5">
                        <div class="font-semibold text-rose-300">Validation Failed:</div>
                        <ul class="list-disc list-inside text-[11px] space-y-0.5 text-rose-200">
                          @for (err of sandboxValidationResult()!.errors; track err) {
                            <li>{{ err }}</li>
                          }
                        </ul>
                      </div>
                    }
                  }
                </div>
              </app-tab>
            </app-tab-group>
          </div>
        }
      </app-slide-over>

      <!-- Schema Create Modal -->
      <app-schema-create-modal
        [isOpen]="isCreateModalOpen()"
        (closed)="isCreateModalOpen.set(false)"
        (schemaCreated)="loadSchemas()">
      </app-schema-create-modal>

      <!-- Schema Version Modal -->
      <app-schema-version-modal
        [isOpen]="isVersionModalOpen()"
        [schema]="selectedSchema()"
        (closed)="isVersionModalOpen.set(false)"
        (versionCreated)="onVersionCreated()">
      </app-schema-version-modal>
    </div>
  `
})
export class SchemasComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly schemaService = inject(EventSchemaService);
  private readonly toast = inject(ToastService);

  readonly schemas = signal<EventSchemaSummary[]>([]);
  readonly filteredSchemas = signal<EventSchemaSummary[]>([]);
  readonly isLoading = signal<boolean>(true);

  readonly isCreateModalOpen = signal<boolean>(false);
  readonly isVersionModalOpen = signal<boolean>(false);
  readonly isDrawerOpen = signal<boolean>(false);

  readonly selectedSchema = signal<EventSchemaDetail | null>(null);
  readonly schemaDocs = signal<SchemaDocumentationResponse | null>(null);
  readonly isLoadingDocs = signal<boolean>(false);

  readonly driftReport = signal<DetectSchemaDriftResponse | null>(null);
  readonly isRunningDriftAudit = signal<boolean>(false);

  readonly sandboxPayload = signal<string>('{\n  "orderId": "11111111-2222-3333-4444-555555555555",\n  "amount": 100.0\n}');
  readonly sandboxValidationResult = signal<ValidateEventPayloadResponse | null>(null);
  readonly isValidatingSandbox = signal<boolean>(false);

  activeTabId: string | null = 'versions';
  searchQuery = '';
  selectedCompatFilter = signal<string>('All');
  selectedStatusFilter = signal<string>('All');

  readonly compatFilterOptions = ['All', 'Backward', 'Forward', 'Full', 'None'];
  readonly statusFilterOptions = ['All', 'Active', 'Draft', 'Deprecated', 'Archived'];

  readonly totalSchemasCount = computed(() => this.schemas().length);
  readonly activeSchemasCount = computed(() => this.schemas().filter(s => s.status === 'Active').length);
  readonly totalVersionsCount = computed(() => this.schemas().reduce((acc, s) => acc + s.totalVersions, 0));
  readonly strictPoliciesCount = computed(() => this.schemas().filter(s => s.compatibilityMode !== 'None').length);

  drawerTitle(): string {
    return this.selectedSchema() ? `Schema: ${this.selectedSchema()!.eventType}` : 'Schema Inspector';
  }

  ngOnInit(): void {
    this.loadSchemas();
  }

  loadSchemas(): void {
    this.isLoading.set(true);
    this.schemaService.getSchemas().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => {
        this.schemas.set(data);
        this.filterSchemas();
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.toast.error('Failed to Load', err.error?.detail || 'Could not fetch event schemas.');
      }
    });
  }

  filterSchemas(): void {
    let list = this.schemas();
    const query = this.searchQuery.trim().toLowerCase();

    if (query) {
      list = list.filter(s => s.eventType.toLowerCase().includes(query) || s.name.toLowerCase().includes(query));
    }

    if (this.selectedCompatFilter() !== 'All') {
      list = list.filter(s => s.compatibilityMode === this.selectedCompatFilter());
    }

    if (this.selectedStatusFilter() !== 'All') {
      list = list.filter(s => s.status === this.selectedStatusFilter());
    }

    this.filteredSchemas.set(list);
  }

  setCompatFilter(mode: string): void {
    this.selectedCompatFilter.set(mode);
    this.filterSchemas();
  }

  setStatusFilter(status: string): void {
    this.selectedStatusFilter.set(status);
    this.filterSchemas();
  }

  openCreateModal(): void {
    this.isCreateModalOpen.set(true);
  }

  openVersionModal(): void {
    this.isVersionModalOpen.set(true);
  }

  inspectSchema(id: string): void {
    this.schemaService.getSchemaById(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (detail) => {
        this.selectedSchema.set(detail);
        this.isDrawerOpen.set(true);
        this.driftReport.set(null);
        this.sandboxValidationResult.set(null);
        this.loadDocs(id);
      },
      error: (err) => {
        this.toast.error('Error', err.error?.detail || 'Failed to retrieve schema details.');
      }
    });
  }

  openDriftAuditDirect(summary: EventSchemaSummary): void {
    this.inspectSchema(summary.id);
    this.activeTabId = 'drift';
  }

  closeDrawer(): void {
    this.isDrawerOpen.set(false);
    this.selectedSchema.set(null);
  }

  loadDocs(schemaId: string): void {
    this.isLoadingDocs.set(true);
    this.schemaService.getSchemaDocs(schemaId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (docs) => {
        this.schemaDocs.set(docs);
        this.isLoadingDocs.set(false);
      },
      error: () => {
        this.isLoadingDocs.set(false);
      }
    });
  }

  activateVersion(versionId: string): void {
    if (!this.selectedSchema()) return;
    this.schemaService.activateVersion(this.selectedSchema()!.id, versionId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Activated', 'Schema version set as active.');
        this.inspectSchema(this.selectedSchema()!.id);
        this.loadSchemas();
      },
      error: (err) => {
        this.toast.error('Activation Failed', err.error?.detail || 'Failed to activate version.');
      }
    });
  }

  deprecateVersion(versionId: string): void {
    if (!this.selectedSchema()) return;
    this.schemaService.deprecateVersion(this.selectedSchema()!.id, versionId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Deprecated', 'Schema version marked as deprecated.');
        this.inspectSchema(this.selectedSchema()!.id);
        this.loadSchemas();
      },
      error: (err) => {
        this.toast.error('Deprecation Failed', err.error?.detail || 'Failed to deprecate version.');
      }
    });
  }

  onVersionCreated(): void {
    if (this.selectedSchema()) {
      this.inspectSchema(this.selectedSchema()!.id);
    }
    this.loadSchemas();
  }

  runDriftAudit(): void {
    if (!this.selectedSchema()) return;
    this.isRunningDriftAudit.set(true);
    this.schemaService.detectDrift(this.selectedSchema()!.id, 50).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (report) => {
        this.driftReport.set(report);
        this.isRunningDriftAudit.set(false);
      },
      error: (err) => {
        this.isRunningDriftAudit.set(false);
        this.toast.error('Audit Failed', err.error?.detail || 'Failed to run drift audit.');
      }
    });
  }

  loadSamplePayloadIntoSandbox(): void {
    if (this.schemaDocs()?.samplePayloadJson) {
      this.sandboxPayload.set(this.schemaDocs()!.samplePayloadJson);
    }
  }

  validateSandboxPayload(): void {
    if (!this.selectedSchema()) return;
    this.isValidatingSandbox.set(true);
    this.schemaService.validatePayload({
      schemaId: this.selectedSchema()!.id,
      payloadJson: this.sandboxPayload()
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isValidatingSandbox.set(false);
        this.sandboxValidationResult.set(res);
      },
      error: (err) => {
        this.isValidatingSandbox.set(false);
        this.toast.error('Validation Error', err.error?.detail || 'Failed to validate payload.');
      }
    });
  }

  deleteSchema(summary: EventSchemaSummary): void {
    if (!confirm(`Are you sure you want to delete schema '${summary.name}' (${summary.eventType}) and all its versions?`)) {
      return;
    }

    this.schemaService.deleteSchema(summary.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Schema Deleted', `Event schema '${summary.name}' was removed.`);
        if (this.selectedSchema()?.id === summary.id) {
          this.closeDrawer();
        }
        this.loadSchemas();
      },
      error: (err) => {
        this.toast.error('Delete Failed', err.error?.detail || 'Failed to delete schema.');
      }
    });
  }

  copyToClipboard(text: string, label: string): void {
    navigator.clipboard.writeText(text);
    this.toast.success('Copied', `${label} copied to clipboard.`);
  }
}
