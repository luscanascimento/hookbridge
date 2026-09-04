import {
  Component,
  contentChildren,
  effect,
  input,
  model,
  output,
  computed
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { TabComponent } from './tab.component';

export type TabGroupVariant = 'underline' | 'pills';

@Component({
  selector: 'app-tab-group',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="w-full flex flex-col">
      <!-- Tabs Header Navigation -->
      <div
        role="tablist"
        [attr.aria-label]="ariaLabel()"
        class="flex items-center"
        [ngClass]="headerContainerClasses()">
        @for (tab of tabs(); track tab.id()) {
          <button
            type="button"
            role="tab"
            [id]="'tab-' + tab.id()"
            [attr.aria-selected]="activeId() === tab.id()"
            [attr.aria-controls]="'panel-' + tab.id()"
            [disabled]="tab.disabled()"
            [tabindex]="activeId() === tab.id() ? 0 : -1"
            (click)="selectTab(tab.id())"
            (keydown)="onKeydown($event, tab.id())"
            [ngClass]="[
              'inline-flex items-center gap-2 text-xs font-medium transition-all select-none focus:outline-none',
              tabButtonClasses(tab.id(), tab.disabled())
            ]">
            @if (tab.icon()) {
              <span class="w-4 h-4 flex items-center justify-center">{{ tab.icon() }}</span>
            }
            <span>{{ tab.label() }}</span>
            @if (tab.badge() !== null && tab.badge() !== undefined) {
              <span
                class="px-1.5 py-0.5 rounded-full text-[10px] font-mono leading-none"
                [ngClass]="activeId() === tab.id() ? 'bg-brand-500/20 text-brand-300' : 'bg-surface-800 text-surface-400'">
                {{ tab.badge() }}
              </span>
            }
          </button>
        }
      </div>

      <!-- Tab Content Area -->
      <div class="mt-4 w-full">
        <ng-content></ng-content>
      </div>
    </div>
  `
})
export class TabGroupComponent {
  readonly variant = input<TabGroupVariant>('underline');
  readonly ariaLabel = input<string>('Tabs');
  readonly activeTab = model<string | null>(null);

  readonly tabChange = output<string>();

  readonly tabs = contentChildren(TabComponent);

  readonly activeId = computed(() => {
    const selected = this.activeTab();
    const allTabs = this.tabs();
    if (selected && allTabs.some(t => t.id() === selected)) {
      return selected;
    }
    return allTabs.length > 0 ? allTabs[0].id() : null;
  });

  constructor() {
    effect(() => {
      const currentActive = this.activeId();
      for (const tab of this.tabs()) {
        tab.isActive.set(tab.id() === currentActive);
      }
    });
  }

  readonly headerContainerClasses = computed(() => {
    switch (this.variant()) {
      case 'pills':
        return 'p-1 gap-1.5 bg-surface-900 border border-surface-800 rounded-xl max-w-fit';
      case 'underline':
      default:
        return 'border-b border-surface-800 gap-6';
    }
  });

  tabButtonClasses(tabId: string, disabled: boolean): string {
    const isSelected = this.activeId() === tabId;
    if (disabled) {
      return 'opacity-40 cursor-not-allowed text-surface-500';
    }

    if (this.variant() === 'pills') {
      return isSelected
        ? 'bg-surface-800 text-white shadow-xs px-3 py-1.5 rounded-lg font-semibold'
        : 'text-surface-400 hover:text-surface-200 hover:bg-surface-800/50 px-3 py-1.5 rounded-lg';
    }

    // Underline variant
    return isSelected
      ? 'border-b-2 border-brand-500 text-brand-400 pb-3 -mb-[1px] font-semibold'
      : 'border-b-2 border-transparent text-surface-400 hover:text-surface-200 hover:border-surface-700 pb-3 -mb-[1px]';
  }

  selectTab(tabId: string): void {
    const tab = this.tabs().find(t => t.id() === tabId);
    if (!tab || tab.disabled()) return;

    this.activeTab.set(tabId);
    this.tabChange.emit(tabId);
  }

  onKeydown(event: KeyboardEvent, currentTabId: string): void {
    const availableTabs = this.tabs().filter(t => !t.disabled());
    if (availableTabs.length === 0) return;

    const currentIndex = availableTabs.findIndex(t => t.id() === currentTabId);
    let nextIndex = -1;

    switch (event.key) {
      case 'ArrowRight':
      case 'ArrowDown':
        event.preventDefault();
        nextIndex = (currentIndex + 1) % availableTabs.length;
        break;
      case 'ArrowLeft':
      case 'ArrowUp':
        event.preventDefault();
        nextIndex = (currentIndex - 1 + availableTabs.length) % availableTabs.length;
        break;
      case 'Home':
        event.preventDefault();
        nextIndex = 0;
        break;
      case 'End':
        event.preventDefault();
        nextIndex = availableTabs.length - 1;
        break;
    }

    if (nextIndex !== -1) {
      const targetTab = availableTabs[nextIndex];
      this.selectTab(targetTab.id());
      const el = document.getElementById('tab-' + targetTab.id());
      el?.focus();
    }
  }
}
