import { Component, input, model, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface FilterOption {
  label: string;
  value: string;
}

export interface FilterConfig {
  id: string;
  label: string;
  options: FilterOption[];
}

@Component({
  selector: 'app-search-filter-bar',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="flex flex-col gap-3 w-full bg-surface-900/60 border border-surface-800/80 p-3 rounded-xl shadow-xs">
      <div class="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
        <!-- Search Input -->
        <div class="relative flex-1">
          <div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-surface-500">
            <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
            </svg>
          </div>
          <input
            type="text"
            [ngModel]="searchQuery()"
            (ngModelChange)="onSearchInput($event)"
            [placeholder]="searchPlaceholder()"
            class="block w-full pl-9 pr-8 py-1.5 text-xs bg-surface-950 border border-surface-800 rounded-lg text-surface-200 placeholder-surface-500 focus:outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500 transition-all font-sans" />
          @if (searchQuery()) {
            <button
              (click)="clearSearch()"
              type="button"
              class="absolute inset-y-0 right-0 pr-2.5 flex items-center text-surface-500 hover:text-surface-300">
              <svg class="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          }
        </div>

        <!-- Filter Dropdowns & Actions -->
        <div class="flex items-center gap-2 flex-wrap">
          @for (filter of filters(); track filter.id) {
            <div class="relative">
              <select
                [ngModel]="activeFilters()[filter.id] || ''"
                (ngModelChange)="onFilterSelect(filter.id, $event)"
                class="appearance-none bg-surface-950 border border-surface-800 text-surface-300 text-xs rounded-lg pl-3 pr-8 py-1.5 focus:outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500 cursor-pointer transition-colors">
                <option value="">{{ filter.label }}: All</option>
                @for (opt of filter.options; track opt.value) {
                  <option [value]="opt.value">{{ opt.label }}</option>
                }
              </select>
              <div class="pointer-events-none absolute inset-y-0 right-0 flex items-center px-2 text-surface-500">
                <svg class="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
                </svg>
              </div>
            </div>
          }

          <!-- Refresh Button -->
          @if (showRefresh()) {
            <button
              (click)="onRefresh()"
              [disabled]="loading()"
              type="button"
              title="Refresh results"
              class="p-1.5 rounded-lg bg-surface-950 border border-surface-800 text-surface-400 hover:text-white hover:bg-surface-800 transition-colors disabled:opacity-50">
              <svg class="h-4 w-4" [ngClass]="{'animate-spin': loading()}" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
              </svg>
            </button>
          }

          <!-- Slot for Extra Actions -->
          <ng-content select="[slot=actions]"></ng-content>
        </div>
      </div>

      <!-- Active Filter Chips / Badges (if any active filters or search) -->
      @if (hasActiveFilters()) {
        <div class="flex items-center gap-2 pt-2 border-t border-surface-800/60 flex-wrap text-xs">
          <span class="text-surface-500 text-[11px]">Active Filters:</span>

          @if (searchQuery()) {
            <span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-brand-950/60 border border-brand-800/60 text-brand-300 text-[11px]">
              <span>Query: "{{ searchQuery() }}"</span>
              <button (click)="clearSearch()" class="hover:text-white">✕</button>
            </span>
          }

          @for (filter of filters(); track filter.id) {
            @if (activeFilters()[filter.id]) {
              <span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-surface-800 border border-surface-700 text-surface-300 text-[11px]">
                <span>{{ filter.label }}: {{ getOptionLabel(filter, activeFilters()[filter.id]) }}</span>
                <button (click)="clearFilter(filter.id)" class="hover:text-white">✕</button>
              </span>
            }
          }

          <button
            (click)="resetAll()"
            class="text-[11px] text-surface-400 hover:text-rose-400 underline underline-offset-2 ml-auto cursor-pointer">
            Clear all
          </button>
        </div>
      }
    </div>
  `
})
export class SearchFilterBarComponent {
  readonly searchPlaceholder = input<string>('Search...');
  readonly filters = input<FilterConfig[]>([]);
  readonly showRefresh = input<boolean>(true);
  readonly loading = input<boolean>(false);

  readonly searchQuery = model<string>('');
  readonly activeFilters = model<Record<string, string>>({});

  readonly searchChange = output<string>();
  readonly filterChange = output<{ id: string; value: string }>();
  readonly refresh = output<void>();
  readonly reset = output<void>();

  private debounceTimer: any = null;

  onSearchInput(value: string): void {
    this.searchQuery.set(value);
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
    }
    this.debounceTimer = setTimeout(() => {
      this.searchChange.emit(value);
    }, 300);
  }

  clearSearch(): void {
    this.searchQuery.set('');
    this.searchChange.emit('');
  }

  onFilterSelect(filterId: string, value: string): void {
    const current = { ...this.activeFilters() };
    if (value) {
      current[filterId] = value;
    } else {
      delete current[filterId];
    }
    this.activeFilters.set(current);
    this.filterChange.emit({ id: filterId, value });
  }

  clearFilter(filterId: string): void {
    this.onFilterSelect(filterId, '');
  }

  hasActiveFilters(): boolean {
    return Boolean(this.searchQuery()) || Object.keys(this.activeFilters()).length > 0;
  }

  getOptionLabel(filter: FilterConfig, value: string): string {
    const opt = filter.options.find(o => o.value === value);
    return opt ? opt.label : value;
  }

  resetAll(): void {
    this.searchQuery.set('');
    this.activeFilters.set({});
    this.searchChange.emit('');
    this.reset.emit();
  }

  onRefresh(): void {
    this.refresh.emit();
  }
}
