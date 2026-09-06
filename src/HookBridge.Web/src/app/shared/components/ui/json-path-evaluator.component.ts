import { Component, input, signal, inject, output, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PayloadService } from '../../../core/services/payload.service';
import { JsonPathMatchItem, JsonPathEvaluation } from '../../../core/models/payload.models';

@Component({
  selector: 'app-json-path-evaluator',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="rounded-xl border border-surface-800 bg-surface-950 font-mono text-xs overflow-hidden shadow-inner flex flex-col space-y-4 p-4">
      <!-- Input Box & Presets Bar -->
      <div class="space-y-2">
        <div class="flex items-center justify-between">
          <label class="text-[11px] font-sans font-semibold text-surface-300 uppercase tracking-wider flex items-center gap-1.5">
            <svg class="w-4 h-4 text-brand-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
            </svg>
            JSONPath Expression Evaluator
          </label>

          @if (result()?.isValid && (result()?.matchCount || 0) > 0) {
            <span class="text-[11px] font-sans px-2 py-0.5 rounded bg-emerald-500/15 text-emerald-300 border border-emerald-500/30">
              {{ result()?.matchCount }} match{{ result()?.matchCount === 1 ? '' : 'es' }}
            </span>
          }
        </div>

        <div class="flex items-center gap-2">
          <div class="relative flex-1">
            <span class="absolute left-3 top-2.5 text-surface-500 font-mono text-xs select-none">expr:</span>
            <input
              type="text"
              [(ngModel)]="queryPath"
              (keydown.enter)="evaluate()"
              placeholder="e.g. $.data.object.amount or $..email"
              class="w-full bg-surface-900 border border-surface-700 rounded-lg pl-14 pr-4 py-2 text-white font-mono text-xs focus:border-brand-500 focus:outline-none placeholder:text-surface-600 shadow-inner" />
          </div>

          <button
            type="button"
            (click)="evaluate()"
            [disabled]="isEvaluating()"
            class="px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white font-sans text-xs font-semibold rounded-lg shadow transition-colors shrink-0">
            {{ isEvaluating() ? 'Evaluating...' : 'Query' }}
          </button>
        </div>

        <!-- Quick Presets -->
        <div class="flex flex-wrap items-center gap-1.5 pt-1">
          <span class="text-[10px] font-sans text-surface-500 uppercase mr-1">Presets:</span>
          @for (preset of presets; track preset.expr) {
            <button
              type="button"
              (click)="applyPreset(preset.expr)"
              class="px-2 py-0.5 rounded bg-surface-900 hover:bg-surface-800 text-surface-300 hover:text-white border border-surface-800 text-[11px] font-mono transition-colors">
              {{ preset.label }}
            </button>
          }
        </div>
      </div>

      <!-- Error State -->
      @if (result()?.errorMessage) {
        <div class="p-3 bg-rose-950/30 border border-rose-800/40 rounded-lg text-xs font-mono text-rose-300 flex items-center gap-2">
          <svg class="w-4 h-4 text-rose-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
          </svg>
          <span>{{ result()?.errorMessage }}</span>
        </div>
      }

      <!-- Evaluation Matches Results List -->
      @if (result()?.matches?.length; as count) {
        <div class="border border-surface-800 rounded-lg overflow-hidden bg-surface-900/40">
          <div class="px-3 py-1.5 bg-surface-900/80 border-b border-surface-800 text-[11px] font-sans font-medium text-surface-400 flex items-center justify-between">
            <span>Query Results ({{ count }})</span>
            <button
              type="button"
              (click)="highlightAllMatches()"
              class="text-brand-400 hover:text-brand-300 text-[10px] font-sans transition-colors">
              Highlight all in Tree
            </button>
          </div>

          <div class="divide-y divide-surface-800/60 max-h-64 overflow-y-auto">
            @for (match of result()?.matches; track match.path) {
              <div class="p-2.5 flex items-start justify-between gap-3 hover:bg-surface-800/40 transition-colors group">
                <div class="space-y-1 min-w-0">
                  <div class="flex items-center gap-2">
                    <span class="text-purple-300 font-bold select-all">{{ match.path }}</span>
                    <span class="px-1.5 py-0.2 rounded text-[9px] font-sans uppercase font-bold" [ngClass]="getTypeBadgeClass(match.valueType)">
                      {{ match.valueType }}
                    </span>
                  </div>
                  <div class="text-surface-200 break-all select-all font-mono text-xs">
                    {{ match.valueJson }}
                  </div>
                </div>

                <div class="flex items-center gap-1.5 shrink-0 opacity-0 group-hover:opacity-100 transition-opacity">
                  <button
                    type="button"
                    (click)="copyText(match.valueJson, match.path)"
                    class="px-2 py-0.5 rounded bg-surface-800 hover:bg-surface-700 text-[10px] font-sans text-surface-300 hover:text-white transition-colors">
                    {{ copiedKey() === match.path ? 'Copied!' : 'Copy' }}
                  </button>
                  <button
                    type="button"
                    (click)="selectPath(match.path)"
                    class="px-2 py-0.5 rounded bg-brand-600/20 hover:bg-brand-600/30 text-[10px] font-sans text-brand-300 border border-brand-500/30 transition-colors">
                    Locate
                  </button>
                </div>
              </div>
            }
          </div>
        </div>
      } @else if (hasSearched() && !result()?.errorMessage) {
        <div class="p-6 text-center text-surface-500 font-sans text-xs">
          No matches found for expression <span class="font-mono text-surface-300">"{{ queryPath() }}"</span>
        </div>
      }
    </div>
  `
})
export class JsonPathEvaluatorComponent {
  private readonly payloadService = inject(PayloadService);

  readonly payloadJson = input.required<string>();
  readonly pathSelected = output<string>();
  readonly matchesFound = output<string[]>();

  readonly queryPath = signal<string>('$.');
  readonly isEvaluating = signal<boolean>(false);
  readonly hasSearched = signal<boolean>(false);
  readonly result = signal<JsonPathEvaluation | null>(null);
  readonly copiedKey = signal<string | null>(null);

  readonly presets = [
    { label: 'Root ID', expr: '$.id' },
    { label: 'Event Type', expr: '$.type' },
    { label: 'All IDs ($..id)', expr: '$..id' },
    { label: 'All Emails ($..email)', expr: '$..email' },
    { label: 'All Items ($..items[*])', expr: '$..items[*]' },
    { label: 'Root Keys ($.*)', expr: '$.*' }
  ];

  evaluate(): void {
    const json = this.payloadJson();
    const query = this.queryPath().trim();
    if (!json || !query) return;

    this.isEvaluating.set(true);
    this.hasSearched.set(true);

    this.payloadService.evaluateJsonPath(json, query).subscribe({
      next: (res) => {
        this.result.set(res);
        this.isEvaluating.set(false);
        if (res.isValid && res.matches.length > 0) {
          const paths = res.matches.map(m => m.path);
          this.matchesFound.emit(paths);
        }
      },
      error: (err) => {
        console.error('JSONPath evaluation error:', err);
        this.result.set({
          isValid: false,
          errorMessage: err.error?.detail || 'Failed to evaluate JSONPath expression.',
          matchCount: 0,
          matches: []
        });
        this.isEvaluating.set(false);
      }
    });
  }

  applyPreset(expr: string): void {
    this.queryPath.set(expr);
    this.evaluate();
  }

  highlightAllMatches(): void {
    const matches = this.result()?.matches || [];
    this.matchesFound.emit(matches.map(m => m.path));
  }

  selectPath(path: string): void {
    this.pathSelected.emit(path);
  }

  getTypeBadgeClass(type: string): string {
    switch (type.toLowerCase()) {
      case 'string': return 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30';
      case 'number': return 'bg-amber-500/20 text-amber-300 border border-amber-500/30';
      case 'boolean': return 'bg-sky-500/20 text-sky-300 border border-sky-500/30';
      case 'object': return 'bg-purple-500/20 text-purple-300 border border-purple-500/30';
      case 'array': return 'bg-indigo-500/20 text-indigo-300 border border-indigo-500/30';
      default: return 'bg-surface-800 text-surface-400 border border-surface-700';
    }
  }

  async copyText(text: string, key: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.copiedKey.set(key);
      setTimeout(() => this.copiedKey.set(null), 2000);
    } catch (err) {
      console.error('Failed to copy text:', err);
    }
  }
}
