import { Component, computed, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-code-viewer',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="rounded-xl border border-surface-800 bg-surface-950 overflow-hidden font-mono text-xs shadow-inner">
      <!-- Toolbar -->
      <div class="px-4 py-2 border-b border-surface-800/80 bg-surface-900/60 flex items-center justify-between">
        <div class="flex items-center gap-2">
          <span class="text-surface-400 text-[11px] font-sans font-medium uppercase tracking-wider">{{ language() }}</span>
          @if (title()) {
            <span class="text-surface-600 font-sans">|</span>
            <span class="text-surface-300 font-sans text-[11px] truncate max-w-xs">{{ title() }}</span>
          }
        </div>

        <div class="flex items-center gap-2">
          <!-- Wrap toggle -->
          <button
            (click)="toggleWrap()"
            class="text-[11px] font-sans px-2 py-0.5 rounded text-surface-400 hover:text-white hover:bg-surface-800 transition-colors"
            [title]="wrapLines() ? 'Disable word wrap' : 'Enable word wrap'">
            {{ wrapLines() ? 'Unwrap' : 'Wrap' }}
          </button>

          <!-- Copy Button -->
          <button
            (click)="copyToClipboard()"
            class="text-[11px] font-sans inline-flex items-center gap-1.5 px-2 py-0.5 rounded bg-surface-800 hover:bg-surface-700 text-surface-200 hover:text-white transition-colors">
            @if (copied()) {
              <svg class="w-3.5 h-3.5 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/>
              </svg>
              <span class="text-emerald-300 font-medium">Copied</span>
            } @else {
              <svg class="w-3.5 h-3.5 text-surface-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 002-2M8 5a2 2 0 012-2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10a2 2 0 00-2 2v3a2 2 0 002 2h10a2 2 0 002-2v-3a2 2 0 00-2-2z"/>
              </svg>
              <span>Copy</span>
            }
          </button>
        </div>
      </div>

      <!-- Code Area -->
      <div class="overflow-x-auto max-h-[450px] p-4 text-surface-200">
        <pre [ngClass]="{'whitespace-pre-wrap break-all': wrapLines(), 'whitespace-pre': !wrapLines()}"><code class="text-surface-200 font-mono">{{ formattedContent() }}</code></pre>
      </div>
    </div>
  `
})
export class CodeViewerComponent {
  readonly code = input.required<string | object | null | undefined>();
  readonly language = input<string>('json');
  readonly title = input<string | null>(null);

  readonly copied = signal<boolean>(false);
  readonly wrapLines = signal<boolean>(true);

  readonly formattedContent = computed(() => {
    const raw = this.code();
    if (raw === null || raw === undefined) return '';

    if (typeof raw === 'object') {
      try {
        return JSON.stringify(raw, null, 2);
      } catch {
        return String(raw);
      }
    }

    if (typeof raw === 'string') {
      const trimmed = raw.trim();
      if ((trimmed.startsWith('{') && trimmed.endsWith('}')) || (trimmed.startsWith('[') && trimmed.endsWith(']'))) {
        try {
          const parsed = JSON.parse(trimmed);
          return JSON.stringify(parsed, null, 2);
        } catch {
          return raw;
        }
      }
      return raw;
    }

    return String(raw);
  });

  toggleWrap(): void {
    this.wrapLines.update(w => !w);
  }

  async copyToClipboard(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.formattedContent());
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    } catch (err) {
      console.error('Failed to copy to clipboard:', err);
    }
  }
}
