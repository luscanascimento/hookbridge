import { Component, ContentChild, TemplateRef, computed, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SkeletonLoaderComponent } from './skeleton-loader.component';
import { EmptyStateComponent } from './empty-state.component';

export interface TableColumn<T = any> {
  key: string;
  label: string;
  sortable?: boolean;
  width?: string;
  align?: 'left' | 'center' | 'right';
  cellRenderer?: (row: T) => string | number | null | undefined;
}

export type SortDirection = 'asc' | 'desc';

@Component({
  selector: 'app-data-table',
  standalone: true,
  imports: [CommonModule, SkeletonLoaderComponent, EmptyStateComponent],
  template: `
    <div class="bg-surface-900/80 border border-surface-800 rounded-xl overflow-hidden shadow-lg shadow-black/20 flex flex-col">
      <!-- Table Container -->
      <div class="overflow-x-auto min-w-full">
        <table class="w-full text-left border-collapse text-xs">
          <!-- Table Header -->
          <thead>
            <tr class="border-b border-surface-800 bg-surface-950/60 text-surface-400 uppercase tracking-wider font-semibold select-none">
              @for (col of columns(); track col.key) {
                <th
                  class="p-3.5"
                  [ngClass]="[
                    col.sortable ? 'cursor-pointer hover:text-white transition-colors' : '',
                    col.align === 'right' ? 'text-right' : (col.align === 'center' ? 'text-center' : 'text-left')
                  ]"
                  [style.width]="col.width"
                  (click)="onSort(col)">
                  <div class="inline-flex items-center gap-1.5"
                       [ngClass]="{'justify-end': col.align === 'right', 'justify-center': col.align === 'center'}">
                    <span>{{ col.label }}</span>
                    @if (col.sortable) {
                      <span class="text-[10px]">
                        @if (sortKey() === col.key) {
                          @if (sortDirection() === 'asc') {
                            ▲
                          } @else {
                            ▼
                          }
                        } @else {
                          <span class="text-surface-600">↕</span>
                        }
                      </span>
                    }
                  </div>
                </th>
              }
            </tr>
          </thead>

          <!-- Table Body -->
          <tbody class="divide-y divide-surface-800/60 font-sans">
            @if (loading()) {
              @for (i of skeletonRows(); track i) {
                <tr class="animate-pulse">
                  @for (col of columns(); track col.key) {
                    <td class="p-3.5">
                      <app-skeleton-loader customClass="h-4 w-3/4"></app-skeleton-loader>
                    </td>
                  }
                </tr>
              }
            } @else if (data().length === 0) {
              <tr>
                <td [attr.colspan]="columns().length" class="p-0">
                  <app-empty-state
                    [title]="emptyTitle()"
                    [description]="emptyDescription()">
                  </app-empty-state>
                </td>
              </tr>
            } @else {
              @for (row of data(); track trackByFn($index, row)) {
                <tr
                  class="hover:bg-surface-800/40 transition-colors"
                  [ngClass]="{'cursor-pointer': interactiveRows()}"
                  (click)="onRowClick(row)">
                  @for (col of columns(); track col.key) {
                    <td
                      class="p-3.5"
                      [ngClass]="col.align === 'right' ? 'text-right' : (col.align === 'center' ? 'text-center' : 'text-left')">
                      @if (cellTemplate) {
                        <ng-container *ngTemplateOutlet="cellTemplate; context: { $implicit: row, column: col, value: row[col.key] }"></ng-container>
                      } @else {
                        <span class="text-surface-200">
                          {{ col.cellRenderer ? col.cellRenderer(row) : row[col.key] }}
                        </span>
                      }
                    </td>
                  }
                </tr>
              }
            }
          </tbody>
        </table>
      </div>

      <!-- Pagination Footer -->
      @if (showPagination() && totalCount() > 0) {
        <div class="p-3.5 border-t border-surface-800/80 bg-surface-950/40 flex items-center justify-between text-xs text-surface-400">
          <div>
            Showing
            <span class="font-medium text-white">{{ startItemIndex() }}</span>
            to
            <span class="font-medium text-white">{{ endItemIndex() }}</span>
            of
            <span class="font-medium text-white">{{ totalCount() }}</span>
            results
          </div>

          <div class="flex items-center gap-1.5">
            <button
              [disabled]="page() <= 1 || loading()"
              (click)="onPageChange(page() - 1)"
              class="px-2.5 py-1 rounded bg-surface-800 hover:bg-surface-700 text-surface-300 hover:text-white disabled:opacity-40 disabled:cursor-not-allowed transition-colors">
              Previous
            </button>
            <span class="px-2 font-mono text-surface-400">Page {{ page() }} of {{ totalPages() }}</span>
            <button
              [disabled]="page() >= totalPages() || loading()"
              (click)="onPageChange(page() + 1)"
              class="px-2.5 py-1 rounded bg-surface-800 hover:bg-surface-700 text-surface-300 hover:text-white disabled:opacity-40 disabled:cursor-not-allowed transition-colors">
              Next
            </button>
          </div>
        </div>
      }
    </div>
  `
})
export class DataTableComponent<T extends Record<string, any> = any> {
  readonly columns = input.required<TableColumn<T>[]>();
  readonly data = input.required<T[]>();
  readonly loading = input<boolean>(false);
  readonly sortKey = input<string | null>(null);
  readonly sortDirection = input<SortDirection>('asc');
  readonly interactiveRows = input<boolean>(false);

  readonly page = input<number>(1);
  readonly pageSize = input<number>(10);
  readonly totalCount = input<number>(0);
  readonly showPagination = input<boolean>(true);

  readonly emptyTitle = input<string>('No data available');
  readonly emptyDescription = input<string>('There are no items matching this criteria.');

  @ContentChild('cellTemplate', { static: false })
  cellTemplate?: TemplateRef<any>;

  readonly sortChange = output<{ key: string; direction: SortDirection }>();
  readonly pageChange = output<number>();
  readonly rowClick = output<T>();

  readonly skeletonRows = computed(() => Array.from({ length: this.pageSize() > 10 ? 10 : this.pageSize() }, (_, i) => i));

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / (this.pageSize() || 10))));

  readonly startItemIndex = computed(() => {
    if (this.totalCount() === 0) return 0;
    return (this.page() - 1) * this.pageSize() + 1;
  });

  readonly endItemIndex = computed(() => {
    return Math.min(this.page() * this.pageSize(), this.totalCount());
  });

  trackByFn(index: number, item: T): any {
    return item['id'] ?? index;
  }

  onSort(col: TableColumn<T>): void {
    if (!col.sortable) return;

    let nextDir: SortDirection = 'asc';
    if (this.sortKey() === col.key) {
      nextDir = this.sortDirection() === 'asc' ? 'desc' : 'asc';
    }

    this.sortChange.emit({ key: col.key, direction: nextDir });
  }

  onPageChange(newPage: number): void {
    if (newPage >= 1 && newPage <= this.totalPages()) {
      this.pageChange.emit(newPage);
    }
  }

  onRowClick(row: T): void {
    if (this.interactiveRows()) {
      this.rowClick.emit(row);
    }
  }
}
