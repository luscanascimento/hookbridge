import { Component, input, signal, computed, inject, effect, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PayloadService } from '../../../core/services/payload.service';
import { PayloadDiff, PayloadDiffEntry } from '../../../core/models/payload.models';
import { SkeletonLoaderComponent } from './skeleton-loader.component';

@Component({
  selector: 'app-payload-diff-viewer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, SkeletonLoaderComponent],
  template: `
    <div class="rounded-xl border border-surface-800 bg-surface-950 font-mono text-xs overflow-hidden shadow-inner flex flex-col">
      <!-- Toolbar & Statistics Summary Bar -->
      <div class="px-4 py-3 bg-surface-900/80 border-b border-surface-800 flex flex-wrap items-center justify-between gap-3">
        <div class="flex items-center gap-2">
          <span class="text-xs font-sans font-semibold text-surface-200">Structural Payload Diff</span>
          
          @if (diffResult(); as diff) {
            <div class="flex items-center gap-1.5 ml-2">
              <span class="px-2 py-0.5 rounded-full text-[10px] font-sans font-medium bg-emerald-500/15 text-emerald-300 border border-emerald-500/30">
                +{{ diff.addedCount }} added
              </span>
              <span class="px-2 py-0.5 rounded-full text-[10px] font-sans font-medium bg-rose-500/15 text-rose-300 border border-rose-500/30">
                -{{ diff.removedCount }} removed
              </span>
              <span class="px-2 py-0.5 rounded-full text-[10px] font-sans font-medium bg-amber-500/15 text-amber-300 border border-amber-500/30">
                ~{{ diff.modifiedCount }} modified
              </span>
              <span class="px-2 py-0.5 rounded-full text-[10px] font-sans font-medium bg-surface-800 text-surface-400 border border-surface-700">
                {{ diff.unchangedCount }} unchanged
              </span>
            </div>
          }
        </div>

        <div class="flex items-center gap-3">
          <!-- Byte Delta Pill -->
          @if (diffResult(); as diff) {
            <div class="text-[11px] font-mono text-surface-400">
              Delta: <span [ngClass]="{'text-emerald-400': diff.byteSizeDelta > 0, 'text-rose-400': diff.byteSizeDelta < 0, 'text-surface-300': diff.byteSizeDelta === 0}">
                {{ diff.byteSizeDelta > 0 ? '+' : '' }}{{ diff.byteSizeDelta }} bytes
              </span>
            </div>
          }

          <!-- Filter toggle -->
          <button
            type="button"
            (click)="toggleOnlyChanges()"
            class="px-2.5 py-1 text-[11px] font-sans rounded border transition-colors"
            [ngClass]="onlyChanges() ? 'bg-brand-600/20 text-brand-300 border-brand-500/40' : 'bg-surface-800 text-surface-300 border-surface-700 hover:text-white'">
            {{ onlyChanges() ? 'Showing Changes Only' : 'Show All Properties' }}
          </button>
        </div>
      </div>

      <!-- Diff Content Table -->
      @if (isLoading()) {
        <div class="p-6 space-y-3">
          <app-skeleton-loader customClass="h-8 w-full"></app-skeleton-loader>
          <app-skeleton-loader customClass="h-8 w-full"></app-skeleton-loader>
          <app-skeleton-loader customClass="h-8 w-full"></app-skeleton-loader>
        </div>
      } @else if (errorState()) {
        <div class="p-6 text-center text-xs text-rose-400">
          {{ errorState() }}
        </div>
      } @else if (filteredEntries().length === 0) {
        <div class="p-8 text-center text-surface-400 text-xs font-sans">
          @if (diffResult()?.hasDifferences === false) {
            <div class="flex flex-col items-center gap-2">
              <svg class="w-8 h-8 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/>
              </svg>
              <span class="text-surface-200 font-semibold">Identical Payloads</span>
              <span class="text-surface-500 text-[11px]">No structural differences found between left and right payloads.</span>
            </div>
          } @else {
            <span>No changed entries to display with current filters.</span>
          }
        </div>
      } @else {
        <div class="overflow-x-auto max-h-[500px]">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="border-b border-surface-800 bg-surface-900/50 text-[11px] font-sans text-surface-400 uppercase tracking-wider">
                <th class="py-2 px-3 w-12 text-center">Type</th>
                <th class="py-2 px-3 w-1/4">Path</th>
                <th class="py-2 px-3 w-1/3">Original (Left)</th>
                <th class="py-2 px-3 w-1/3">New (Right)</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-surface-800/60 font-mono text-xs">
              @for (entry of filteredEntries(); track entry.path) {
                <tr [ngClass]="getRowBackground(entry.diffType)">
                  <!-- Diff Type Badge -->
                  <td class="py-2 px-3 text-center">
                    <span class="px-1.5 py-0.5 rounded text-[10px] font-bold" [ngClass]="getBadgeClass(entry.diffType)">
                      {{ getDiffSymbol(entry.diffType) }}
                    </span>
                  </td>

                  <!-- Path -->
                  <td class="py-2 px-3 font-semibold text-purple-300 break-all select-all">
                    {{ entry.path }}
                  </td>

                  <!-- Left Value -->
                  <td class="py-2 px-3 text-surface-300 break-all select-all">
                    @if (entry.leftValue) {
                      <span [ngClass]="{'line-through text-rose-400/80': entry.diffType === 'Removed' || entry.diffType === 'Modified'}">
                        {{ entry.leftValue }}
                      </span>
                    } @else {
                      <span class="text-surface-600 italic">--</span>
                    }
                  </td>

                  <!-- Right Value -->
                  <td class="py-2 px-3 text-surface-300 break-all select-all">
                    @if (entry.rightValue) {
                      <span [ngClass]="{'text-emerald-300 font-medium': entry.diffType === 'Added', 'text-amber-300': entry.diffType === 'Modified'}">
                        {{ entry.rightValue }}
                      </span>
                    } @else {
                      <span class="text-surface-600 italic">--</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `
})
export class PayloadDiffViewerComponent {
  private readonly payloadService = inject(PayloadService);

  readonly leftJson = input.required<string>();
  readonly rightJson = input.required<string>();

  readonly isLoading = signal<boolean>(false);
  readonly errorState = signal<string | null>(null);
  readonly diffResult = signal<PayloadDiff | null>(null);
  readonly onlyChanges = signal<boolean>(true);

  constructor() {
    effect(() => {
      const left = this.leftJson();
      const right = this.rightJson();
      if (left && right) {
        this.computeDiff(left, right);
      }
    });
  }

  readonly filteredEntries = computed<PayloadDiffEntry[]>(() => {
    const diff = this.diffResult();
    if (!diff) return [];
    if (!this.onlyChanges()) return diff.entries;
    return diff.entries.filter(e => e.diffType !== 'Unchanged');
  });

  computeDiff(left: string, right: string): void {
    this.isLoading.set(true);
    this.errorState.set(null);

    this.payloadService.diffPayloads(left, right).subscribe({
      next: (res) => {
        this.diffResult.set(res);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Diff error:', err);
        this.errorState.set(err.error?.detail || 'Failed to compare JSON payloads.');
        this.isLoading.set(false);
      }
    });
  }

  toggleOnlyChanges(): void {
    this.onlyChanges.update(v => !v);
  }

  getRowBackground(type: string): string {
    switch (type) {
      case 'Added':
        return 'bg-emerald-950/25 hover:bg-emerald-950/40';
      case 'Removed':
        return 'bg-rose-950/25 hover:bg-rose-950/40';
      case 'Modified':
        return 'bg-amber-950/25 hover:bg-amber-950/40';
      default:
        return 'hover:bg-surface-900/50';
    }
  }

  getBadgeClass(type: string): string {
    switch (type) {
      case 'Added':
        return 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30';
      case 'Removed':
        return 'bg-rose-500/20 text-rose-300 border border-rose-500/30';
      case 'Modified':
        return 'bg-amber-500/20 text-amber-300 border border-amber-500/30';
      default:
        return 'bg-surface-800 text-surface-400 border border-surface-700';
    }
  }

  getDiffSymbol(type: string): string {
    switch (type) {
      case 'Added': return '+';
      case 'Removed': return '-';
      case 'Modified': return '~';
      default: return '=';
    }
  }
}
