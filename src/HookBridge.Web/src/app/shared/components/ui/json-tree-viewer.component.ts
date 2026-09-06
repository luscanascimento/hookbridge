import { Component, input, signal, computed, ChangeDetectionStrategy, output } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface TreeNode {
  key: string;
  path: string;
  value: any;
  type: 'object' | 'array' | 'string' | 'number' | 'boolean' | 'null';
  isExpanded: boolean;
  children?: TreeNode[];
  itemCount?: number;
}

@Component({
  selector: 'app-json-tree-viewer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule],
  template: `
    <div class="rounded-xl border border-surface-800 bg-surface-950/90 font-mono text-xs overflow-hidden shadow-inner flex flex-col">
      <!-- Top Toolbar -->
      <div class="px-4 py-2 bg-surface-900/70 border-b border-surface-800 flex items-center justify-between gap-2 shrink-0">
        <div class="flex items-center gap-3">
          <div class="flex items-center gap-1.5 text-surface-400 text-[11px] font-sans">
            <svg class="w-3.5 h-3.5 text-brand-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h7"/>
            </svg>
            <span class="font-semibold text-surface-200">Interactive JSON Tree</span>
          </div>

          @if (rootNode()) {
            <span class="text-[10px] font-mono px-2 py-0.5 rounded bg-surface-800 text-surface-300 border border-surface-700">
              {{ rootNode()?.type === 'array' ? '[' + rootNode()?.itemCount + ' items]' : '{' + rootNode()?.itemCount + ' keys}' }}
            </span>
          }
        </div>

        <div class="flex items-center gap-2">
          <!-- Expand/Collapse all -->
          <button
            type="button"
            (click)="expandAll()"
            class="px-2 py-1 bg-surface-800 hover:bg-surface-700 text-surface-300 hover:text-white rounded text-[11px] font-sans transition-colors">
            Expand All
          </button>
          <button
            type="button"
            (click)="collapseAll()"
            class="px-2 py-1 bg-surface-800 hover:bg-surface-700 text-surface-300 hover:text-white rounded text-[11px] font-sans transition-colors">
            Collapse All
          </button>

          <!-- Copy JSON Button -->
          <button
            type="button"
            (click)="copyRawJson()"
            class="px-2.5 py-1 bg-brand-600/20 hover:bg-brand-600/30 text-brand-300 border border-brand-500/30 rounded text-[11px] font-sans inline-flex items-center gap-1 transition-colors">
            @if (copiedAll()) {
              <svg class="w-3.5 h-3.5 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/>
              </svg>
              <span>Copied!</span>
            } @else {
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 002-2M8 5a2 2 0 012-2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10a2 2 0 00-2 2v3a2 2 0 002 2h10a2 2 0 002-2v-3a2 2 0 00-2-2z"/>
              </svg>
              <span>Copy JSON</span>
            }
          </button>
        </div>
      </div>

      <!-- Tree Content Body -->
      <div class="p-4 overflow-auto max-h-[550px] space-y-1">
        @if (rootNode(); as root) {
          <ng-container *ngTemplateOutlet="nodeTemplate; context: { $implicit: root, depth: 0 }"></ng-container>
        } @else {
          <div class="text-surface-500 italic p-4 text-center">Empty or invalid JSON payload</div>
        }
      </div>

      <!-- Recursive Node Template -->
      <ng-template #nodeTemplate let-node let-depth="depth">
        <div class="group relative flex flex-col font-mono text-xs">
          <!-- Node Row -->
          <div
            class="flex items-center gap-1 py-0.5 px-1.5 rounded hover:bg-surface-800/60 transition-colors"
            [style.padding-left.px]="depth * 16 + 6"
            [ngClass]="{
              'bg-amber-500/15 border-l-2 border-amber-400': isPathHighlighted(node.path)
            }">
            
            <!-- Expand/Collapse Arrow if branch -->
            @if (node.type === 'object' || node.type === 'array') {
              <button
                type="button"
                (click)="toggleNode(node)"
                class="w-4 h-4 flex items-center justify-center text-surface-400 hover:text-white shrink-0">
                <svg
                  class="w-3 h-3 transition-transform duration-150"
                  [ngClass]="{'rotate-90': node.isExpanded}"
                  fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/>
                </svg>
              </button>
            } @else {
              <span class="w-4 shrink-0"></span>
            }

            <!-- Node Key Name -->
            @if (node.key !== '$') {
              <span class="text-purple-300 font-semibold select-all">{{ node.key }}:</span>
            }

            <!-- Node Value / Summary -->
            @if (node.type === 'object') {
              <span class="text-surface-400 font-mono">&#123;</span>
              @if (!node.isExpanded) {
                <span class="text-[10px] text-surface-500 font-sans px-1.5 py-0.2 bg-surface-900 rounded border border-surface-800">
                  {{ node.itemCount }} keys
                </span>
                <span class="text-surface-400 font-mono">&#125;</span>
              }
            } @else if (node.type === 'array') {
              <span class="text-surface-400 font-mono">&#91;</span>
              @if (!node.isExpanded) {
                <span class="text-[10px] text-surface-500 font-sans px-1.5 py-0.2 bg-surface-900 rounded border border-surface-800">
                  {{ node.itemCount }} items
                </span>
                <span class="text-surface-400 font-mono">&#93;</span>
              }
            } @else if (node.type === 'string') {
              <span class="text-emerald-300 break-all select-all">"{{ node.value }}"</span>
            } @else if (node.type === 'number') {
              <span class="text-amber-300 select-all font-semibold">{{ node.value }}</span>
            } @else if (node.type === 'boolean') {
              <span class="text-sky-300 font-bold select-all">{{ node.value }}</span>
            } @else if (node.type === 'null') {
              <span class="text-rose-400/80 italic select-all">null</span>
            }

            <!-- Path actions on hover -->
            <div class="ml-auto opacity-0 group-hover:opacity-100 flex items-center gap-1 shrink-0 pl-2 transition-opacity">
              <button
                type="button"
                (click)="copyPath(node.path)"
                title="Copy JSONPath ({{ node.path }})"
                class="px-1.5 py-0.5 rounded bg-surface-800 hover:bg-surface-700 text-[10px] font-sans text-surface-300 hover:text-white transition-colors">
                {{ copiedNodePath() === node.path ? 'Copied Path!' : 'Copy Path' }}
              </button>

              <button
                type="button"
                (click)="copyValue(node)"
                title="Copy Value"
                class="px-1.5 py-0.5 rounded bg-surface-800 hover:bg-surface-700 text-[10px] font-sans text-surface-300 hover:text-white transition-colors">
                Copy Val
              </button>
            </div>
          </div>

          <!-- Expanded Children -->
          @if ((node.type === 'object' || node.type === 'array') && node.isExpanded && node.children) {
            <div>
              @for (child of node.children; track child.path) {
                <ng-container *ngTemplateOutlet="nodeTemplate; context: { $implicit: child, depth: depth + 1 }"></ng-container>
              }
              <!-- Closing bracket -->
              <div class="py-0.5 px-1.5 text-surface-400 font-mono" [style.padding-left.px]="depth * 16 + 22">
                {{ node.type === 'object' ? '}' : ']' }}
              </div>
            </div>
          }
        </div>
      </ng-template>
    </div>
  `
})
export class JsonTreeViewerComponent {
  readonly json = input.required<string | object | null | undefined>();
  readonly highlightPaths = input<string[]>([]);
  readonly nodeSelected = output<TreeNode>();

  readonly copiedAll = signal<boolean>(false);
  readonly copiedNodePath = signal<string | null>(null);

  // Tree state
  readonly rootNode = computed<TreeNode | null>(() => {
    const raw = this.json();
    if (!raw) return null;

    let parsed: any;
    if (typeof raw === 'string') {
      try {
        parsed = JSON.parse(raw);
      } catch {
        return null;
      }
    } else {
      parsed = raw;
    }

    return this.buildTree('$', '$', parsed, true);
  });

  private buildTree(key: string, path: string, value: any, isExpanded: boolean = true): TreeNode {
    if (value === null || value === undefined) {
      return { key, path, value: null, type: 'null', isExpanded: false };
    }

    if (Array.isArray(value)) {
      const children = value.map((item, idx) =>
        this.buildTree(`[${idx}]`, `${path}[${idx}]`, item, true)
      );
      return {
        key,
        path,
        value,
        type: 'array',
        isExpanded,
        itemCount: value.length,
        children
      };
    }

    if (typeof value === 'object') {
      const keys = Object.keys(value);
      const children = keys.map(k => {
        const childPath = path === '$' ? `$.${k}` : `${path}.${k}`;
        return this.buildTree(k, childPath, value[k], true);
      });
      return {
        key,
        path,
        value,
        type: 'object',
        isExpanded,
        itemCount: keys.length,
        children
      };
    }

    if (typeof value === 'number') {
      return { key, path, value, type: 'number', isExpanded: false };
    }

    if (typeof value === 'boolean') {
      return { key, path, value, type: 'boolean', isExpanded: false };
    }

    return { key, path, value: String(value), type: 'string', isExpanded: false };
  }

  toggleNode(node: TreeNode): void {
    node.isExpanded = !node.isExpanded;
  }

  expandAll(): void {
    const root = this.rootNode();
    if (root) this.setExpandedRecursively(root, true);
  }

  collapseAll(): void {
    const root = this.rootNode();
    if (root) this.setExpandedRecursively(root, false);
  }

  private setExpandedRecursively(node: TreeNode, expanded: boolean): void {
    if (node.type === 'object' || node.type === 'array') {
      node.isExpanded = expanded;
      node.children?.forEach(c => this.setExpandedRecursively(c, expanded));
    }
  }

  isPathHighlighted(path: string): boolean {
    const list = this.highlightPaths();
    if (!list || list.length === 0) return false;
    return list.some(p => p.toLowerCase() === path.toLowerCase() || path.toLowerCase().startsWith(p.toLowerCase()));
  }

  async copyPath(path: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(path);
      this.copiedNodePath.set(path);
      setTimeout(() => this.copiedNodePath.set(null), 2000);
    } catch (err) {
      console.error('Failed to copy path:', err);
    }
  }

  async copyValue(node: TreeNode): Promise<void> {
    try {
      const valStr = typeof node.value === 'object' ? JSON.stringify(node.value, null, 2) : String(node.value);
      await navigator.clipboard.writeText(valStr);
      this.copiedNodePath.set(node.path);
      setTimeout(() => this.copiedNodePath.set(null), 2000);
    } catch (err) {
      console.error('Failed to copy value:', err);
    }
  }

  async copyRawJson(): Promise<void> {
    const raw = this.json();
    if (!raw) return;
    try {
      const text = typeof raw === 'string' ? raw : JSON.stringify(raw, null, 2);
      await navigator.clipboard.writeText(text);
      this.copiedAll.set(true);
      setTimeout(() => this.copiedAll.set(false), 2000);
    } catch (err) {
      console.error('Failed to copy JSON:', err);
    }
  }
}
