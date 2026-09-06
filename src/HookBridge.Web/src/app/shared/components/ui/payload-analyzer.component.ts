import { Component, input, signal, inject, effect, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PayloadService } from '../../../core/services/payload.service';
import { PayloadAnalysis, PayloadSchemaValidation } from '../../../core/models/payload.models';
import { SkeletonLoaderComponent } from './skeleton-loader.component';
import { CodeViewerComponent } from './code-viewer.component';

@Component({
  selector: 'app-payload-analyzer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, SkeletonLoaderComponent, CodeViewerComponent],
  template: `
    <div class="space-y-6">
      @if (isLoading()) {
        <div class="space-y-4">
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-4">
            <app-skeleton-loader customClass="h-24 w-full"></app-skeleton-loader>
            <app-skeleton-loader customClass="h-24 w-full"></app-skeleton-loader>
            <app-skeleton-loader customClass="h-24 w-full"></app-skeleton-loader>
            <app-skeleton-loader customClass="h-24 w-full"></app-skeleton-loader>
          </div>
          <app-skeleton-loader customClass="h-48 w-full"></app-skeleton-loader>
        </div>
      } @else if (errorState()) {
        <div class="p-6 text-center text-xs text-rose-400 bg-rose-950/20 border border-rose-800/40 rounded-xl">
          {{ errorState() }}
        </div>
      } @else if (analysis(); as data) {
        <!-- Size & Efficiency KPI Grid -->
        <div class="grid grid-cols-2 sm:grid-cols-4 gap-4">
          <!-- Card 1: Raw Byte Size -->
          <div class="p-4 rounded-xl bg-surface-900/80 border border-surface-800 space-y-1">
            <div class="text-[11px] font-medium text-surface-400 uppercase tracking-wider">Raw Payload Size</div>
            <div class="text-xl font-bold text-white font-mono flex items-baseline gap-1">
              <span>{{ formatBytes(data.rawByteSize) }}</span>
              <span class="text-xs text-surface-500 font-normal">({{ data.rawByteSize }} B)</span>
            </div>
            <div class="text-[11px] text-surface-400">
              Minified: <span class="text-surface-200 font-mono">{{ formatBytes(data.minifiedByteSize) }}</span>
            </div>
          </div>

          <!-- Card 2: Estimated Gzip -->
          <div class="p-4 rounded-xl bg-surface-900/80 border border-surface-800 space-y-1">
            <div class="text-[11px] font-medium text-surface-400 uppercase tracking-wider">Gzip Compressed</div>
            <div class="text-xl font-bold text-emerald-400 font-mono flex items-baseline gap-1">
              <span>{{ formatBytes(data.estimatedGzipByteSize) }}</span>
            </div>
            <div class="text-[11px] text-emerald-400/90 font-medium">
              ~{{ data.compressionRatioPercent }}% compression ratio
            </div>
          </div>

          <!-- Card 3: Structural Metrics -->
          <div class="p-4 rounded-xl bg-surface-900/80 border border-surface-800 space-y-1">
            <div class="text-[11px] font-medium text-surface-400 uppercase tracking-wider">Structure & Depth</div>
            <div class="text-xl font-bold text-purple-300 font-mono">
              {{ data.totalKeys }} <span class="text-xs text-surface-400 font-normal">total keys</span>
            </div>
            <div class="text-[11px] text-surface-400">
              Max depth: <span class="text-surface-200 font-mono">{{ data.maxDepth }} levels</span>
            </div>
          </div>

          <!-- Card 4: Encoding & Unicode -->
          <div class="p-4 rounded-xl bg-surface-900/80 border border-surface-800 space-y-1">
            <div class="text-[11px] font-medium text-surface-400 uppercase tracking-wider">Character Encoding</div>
            <div class="text-xl font-bold text-brand-300 font-mono">
              {{ data.characterEncoding }}
            </div>
            <div class="text-[11px]" [ngClass]="data.isMultibyte ? 'text-amber-300 font-medium' : 'text-surface-400'">
              {{ data.isMultibyte ? data.nonAsciiCharacterCount + ' multi-byte chars (Unicode)' : 'Standard ASCII clean' }}
            </div>
          </div>
        </div>

        <!-- Node Types Distribution Breakdown -->
        <div class="p-4 rounded-xl bg-surface-900/50 border border-surface-800 space-y-3">
          <div class="text-[11px] font-semibold text-surface-400 uppercase tracking-wider">Data Types Breakdown</div>
          <div class="grid grid-cols-3 sm:grid-cols-6 gap-2 text-center text-xs">
            <div class="p-2 rounded-lg bg-surface-950 border border-surface-800/80">
              <span class="text-purple-400 font-bold block text-base font-mono">{{ data.objectCount }}</span>
              <span class="text-surface-400 text-[10px] uppercase">Objects</span>
            </div>
            <div class="p-2 rounded-lg bg-surface-950 border border-surface-800/80">
              <span class="text-indigo-400 font-bold block text-base font-mono">{{ data.arrayCount }}</span>
              <span class="text-surface-400 text-[10px] uppercase">Arrays</span>
            </div>
            <div class="p-2 rounded-lg bg-surface-950 border border-surface-800/80">
              <span class="text-emerald-400 font-bold block text-base font-mono">{{ data.stringCount }}</span>
              <span class="text-surface-400 text-[10px] uppercase">Strings</span>
            </div>
            <div class="p-2 rounded-lg bg-surface-950 border border-surface-800/80">
              <span class="text-amber-400 font-bold block text-base font-mono">{{ data.numberCount }}</span>
              <span class="text-surface-400 text-[10px] uppercase">Numbers</span>
            </div>
            <div class="p-2 rounded-lg bg-surface-950 border border-surface-800/80">
              <span class="text-sky-400 font-bold block text-base font-mono">{{ data.booleanCount }}</span>
              <span class="text-surface-400 text-[10px] uppercase">Booleans</span>
            </div>
            <div class="p-2 rounded-lg bg-surface-950 border border-surface-800/80">
              <span class="text-rose-400 font-bold block text-base font-mono">{{ data.nullCount }}</span>
              <span class="text-surface-400 text-[10px] uppercase">Nulls</span>
            </div>
          </div>
        </div>

        <!-- Inferred JSON Schema Section -->
        <div class="space-y-2">
          <div class="flex items-center justify-between">
            <div class="text-[11px] font-semibold text-surface-400 uppercase tracking-wider">Inferred JSON Schema (Draft 2020-12)</div>
          </div>
          <app-code-viewer
            [code]="data.inferredSchemaJson"
            language="json"
            title="Auto-Generated JSON Schema">
          </app-code-viewer>
        </div>

        <!-- Schema Validation Sandbox -->
        <div class="p-4 rounded-xl bg-surface-900/60 border border-surface-800 space-y-4">
          <div class="flex items-center justify-between">
            <div>
              <div class="text-xs font-semibold text-white">Schema Validator Sandbox</div>
              <div class="text-[11px] text-surface-400">Validate this payload against custom JSON Schema definitions:</div>
            </div>

            <button
              type="button"
              (click)="validateCustomSchema()"
              [disabled]="isValidating()"
              class="px-3 py-1.5 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-xs font-semibold rounded-lg shadow transition-colors">
              {{ isValidating() ? 'Validating...' : 'Validate Schema' }}
            </button>
          </div>

          <div class="space-y-1">
            <textarea
              [(ngModel)]="customSchemaInput"
              rows="6"
              placeholder="Paste JSON Schema here..."
              class="w-full bg-surface-950 border border-surface-700 rounded-lg p-3 text-white font-mono text-xs focus:border-brand-500 focus:outline-none placeholder:text-surface-600 shadow-inner resize-y">
            </textarea>
          </div>

          <!-- Validation Result Output -->
          @if (validationResult(); as val) {
            <div class="p-3.5 rounded-lg border text-xs" [ngClass]="val.isValid ? 'bg-emerald-950/20 border-emerald-500/40 text-emerald-300' : 'bg-rose-950/20 border-rose-500/40 text-rose-300'">
              <div class="flex items-center gap-2 font-semibold">
                @if (val.isValid) {
                  <svg class="w-4 h-4 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/>
                  </svg>
                  <span>Payload is valid according to the schema!</span>
                } @else {
                  <svg class="w-4 h-4 text-rose-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>
                  </svg>
                  <span>Schema Validation Failed ({{ val.validationErrors.length }} violations)</span>
                }
              </div>

              @if (val.validationErrors.length > 0) {
                <ul class="list-disc list-inside mt-2 space-y-1 font-mono text-[11px]">
                  @for (err of val.validationErrors; track err) {
                    <li>{{ err }}</li>
                  }
                </ul>
              }

              @if (val.structuralWarnings.length > 0) {
                <div class="mt-3 pt-2 border-t border-surface-800/80 text-amber-300 text-[11px]">
                  <span class="font-semibold uppercase tracking-wider block text-[10px] mb-1">Warnings:</span>
                  <ul class="list-disc list-inside space-y-0.5 font-mono">
                    @for (w of val.structuralWarnings; track w) {
                      <li>{{ w }}</li>
                    }
                  </ul>
                </div>
              }
            </div>
          }
        </div>
      }
    </div>
  `
})
export class PayloadAnalyzerComponent {
  private readonly payloadService = inject(PayloadService);

  readonly payloadJson = input.required<string>();

  readonly isLoading = signal<boolean>(false);
  readonly errorState = signal<string | null>(null);
  readonly analysis = signal<PayloadAnalysis | null>(null);
  readonly customSchemaInput = signal<string>('');
  readonly isValidating = signal<boolean>(false);
  readonly validationResult = signal<PayloadSchemaValidation | null>(null);

  constructor() {
    effect(() => {
      const json = this.payloadJson();
      if (json) {
        this.runAnalysis(json);
      }
    });
  }

  runAnalysis(json: string): void {
    this.isLoading.set(true);
    this.errorState.set(null);

    this.payloadService.analyzePayload(json).subscribe({
      next: (res) => {
        this.analysis.set(res);
        this.customSchemaInput.set(res.inferredSchemaJson);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Payload analysis error:', err);
        this.errorState.set(err.error?.detail || 'Failed to analyze payload structure.');
        this.isLoading.set(false);
      }
    });
  }

  validateCustomSchema(): void {
    const json = this.payloadJson();
    const schema = this.customSchemaInput();
    if (!json || !schema) return;

    this.isValidating.set(true);
    this.payloadService.validateSchema(json, schema).subscribe({
      next: (res) => {
        this.validationResult.set(res);
        this.isValidating.set(false);
      },
      error: (err) => {
        this.validationResult.set({
          isValid: false,
          validationErrors: [err.error?.detail || 'Invalid schema format.'],
          structuralWarnings: []
        });
        this.isValidating.set(false);
      }
    });
  }

  formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(2)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  }
}
