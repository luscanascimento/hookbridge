import { Component, EventEmitter, Input, Output, inject, signal, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ModalComponent, ButtonComponent } from '../../shared/components';
import { EventSchemaService } from '../../core/services/event-schema.service';
import { ToastService } from '../../shared/components/ui/toast/toast.service';
import { EventSchemaDetail, CheckCompatibilityResponse, CreateSchemaVersionRequest } from '../../core/models/event-schema.models';

@Component({
  selector: 'app-schema-version-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, ModalComponent, ButtonComponent],
  template: `
    <app-modal [isOpen]="isOpen" (closed)="onClose()" [title]="modalTitle" size="xl">
      <div class="space-y-4">
        <!-- Summary Info Bar -->
        @if (schema) {
          <div class="bg-surface-900 border border-surface-800 rounded-lg p-3 flex items-center justify-between">
            <div>
              <span class="text-xs text-surface-400">Target Event: </span>
              <span class="text-xs font-mono font-semibold text-brand-300">{{ schema.eventType }}</span>
            </div>
            <div class="flex items-center gap-2">
              <span class="text-xs text-surface-400">Compatibility Mode: </span>
              <span class="text-xs font-mono px-2 py-0.5 rounded bg-surface-800 text-surface-200 border border-surface-700">
                {{ schema.compatibilityMode }}
              </span>
            </div>
          </div>
        }

        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <!-- Version Name -->
          <div>
            <label class="block text-xs font-medium text-surface-300 mb-1">
              Version Tag / Semantic Version <span class="text-rose-400">*</span>
            </label>
            <input type="text" [(ngModel)]="version" placeholder="e.g. 1.1.0, 2.0.0, v2"
                   class="w-full bg-surface-950 border border-surface-700 rounded-lg px-3 py-2 text-sm text-surface-100 font-mono focus:outline-none focus:border-brand-500" />
          </div>

          <!-- Version Notes / Changelog -->
          <div>
            <label class="block text-xs font-medium text-surface-300 mb-1">
              Changelog Notes / Release Description
            </label>
            <input type="text" [(ngModel)]="description" placeholder="e.g. Added optional trackingCode field"
                   class="w-full bg-surface-950 border border-surface-700 rounded-lg px-3 py-2 text-sm text-surface-100 focus:outline-none focus:border-brand-500" />
          </div>
        </div>

        <!-- JSON Schema Editor -->
        <div>
          <div class="flex items-center justify-between mb-1.5">
            <label class="text-xs font-medium text-surface-300">
              New Version JSON Schema (Draft 2020-12) <span class="text-rose-400">*</span>
            </label>
            <div class="flex items-center gap-2">
              <button type="button" (click)="checkCompatibility()" [disabled]="isCheckingCompat()"
                      class="text-xs px-2.5 py-1 rounded bg-surface-800 text-brand-300 hover:bg-surface-700 border border-surface-700 transition-colors">
                @if (isCheckingCompat()) { Checking... } @else { Test Compatibility }
              </button>
              <button type="button" (click)="formatJson()"
                      class="text-xs text-surface-400 hover:text-surface-200 transition-colors">
                Format JSON
              </button>
            </div>
          </div>
          <textarea [(ngModel)]="schemaJson" (ngModelChange)="onJsonChange()"
                    rows="10"
                    placeholder="{ ... JSON Schema ... }"
                    class="w-full bg-surface-950 border border-surface-700 rounded-lg p-3 text-xs font-mono text-surface-200 focus:outline-none focus:border-brand-500 leading-relaxed"></textarea>
        </div>

        <!-- Compatibility Evaluation Result Banner -->
        @if (compatibilityResult()) {
          @if (compatibilityResult()!.isCompatible) {
            <div class="bg-emerald-950/40 border border-emerald-800/60 rounded-lg p-3 text-xs text-emerald-300 flex items-start gap-2">
              <svg class="w-4 h-4 text-emerald-400 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/>
              </svg>
              <div>
                <div class="font-semibold">Compatible with policy {{ compatibilityResult()!.mode }}</div>
                @if (compatibilityResult()!.nonBreakingChanges.length > 0) {
                  <ul class="list-disc list-inside mt-1 text-[11px] text-emerald-400/80 space-y-0.5">
                    @for (item of compatibilityResult()!.nonBreakingChanges; track item) {
                      <li>{{ item }}</li>
                    }
                  </ul>
                }
              </div>
            </div>
          } @else {
            <div class="bg-rose-950/40 border border-rose-800/60 rounded-lg p-3 text-xs text-rose-300 space-y-2">
              <div class="flex items-center gap-2 font-semibold text-rose-300">
                <svg class="w-4 h-4 text-rose-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>
                </svg>
                Breaking Changes Detected (Violates {{ compatibilityResult()!.mode }} Compatibility)
              </div>
              <ul class="list-disc list-inside text-[11px] text-rose-300/90 space-y-1">
                @for (err of compatibilityResult()!.breakingChanges; track err) {
                  <li>{{ err }}</li>
                }
              </ul>

              <div class="pt-2 border-t border-rose-900/50 flex items-center gap-2">
                <input type="checkbox" id="forceCompat" [(ngModel)]="forceOverride"
                       class="rounded bg-surface-950 border-rose-700 text-rose-500 focus:ring-rose-500" />
                <label for="forceCompat" class="text-[11px] text-rose-200 cursor-pointer font-medium">
                  Force override compatibility constraints and release breaking version
                </label>
              </div>
            </div>
          }
        }

        <!-- Active Toggle -->
        <div class="flex items-center gap-2 pt-2">
          <input type="checkbox" id="setActive" [(ngModel)]="setActive"
                 class="rounded bg-surface-950 border-surface-700 text-brand-500 focus:ring-brand-500" />
          <label for="setActive" class="text-xs text-surface-200 cursor-pointer">
            Set as active version immediately upon creation
          </label>
        </div>

        <!-- Modal Actions -->
        <div class="flex items-center justify-end gap-3 pt-4 border-t border-surface-800">
          <app-button variant="outline" (click)="onClose()">Cancel</app-button>
          <app-button variant="primary" [loading]="isSubmitting()" (click)="submit()">
            Create Version
          </app-button>
        </div>
      </div>
    </app-modal>
  `
})
export class SchemaVersionModalComponent implements OnChanges {
  private readonly schemaService = inject(EventSchemaService);
  private readonly toast = inject(ToastService);

  @Input() isOpen = false;
  @Input() schema: EventSchemaDetail | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() versionCreated = new EventEmitter<void>();

  version = '';
  description = '';
  schemaJson = '';
  setActive = true;
  forceOverride = false;

  readonly isSubmitting = signal<boolean>(false);
  readonly isCheckingCompat = signal<boolean>(false);
  readonly compatibilityResult = signal<CheckCompatibilityResponse | null>(null);

  get modalTitle(): string {
    return this.schema ? `Create New Version for ${this.schema.name}` : 'Create Schema Version';
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen'] && this.isOpen && this.schema) {
      this.resetForm();
    }
  }

  resetForm(): void {
    if (!this.schema) return;
    const latest = this.schema.activeVersion || this.schema.versions[0];
    if (latest) {
      this.schemaJson = latest.schemaJson;
      // Propose next semantic version
      this.version = this.proposeNextVersion(latest.version);
    } else {
      this.schemaJson = '{}';
      this.version = '1.0.0';
    }
    this.description = '';
    this.setActive = true;
    this.forceOverride = false;
    this.compatibilityResult.set(null);
  }

  private proposeNextVersion(current: string): string {
    const parts = current.split('.');
    if (parts.length === 3 && !isNaN(Number(parts[1]))) {
      return `${parts[0]}.${Number(parts[1]) + 1}.0`;
    }
    return `${current}-next`;
  }

  onJsonChange(): void {
    this.compatibilityResult.set(null);
  }

  formatJson(): void {
    try {
      const parsed = JSON.parse(this.schemaJson);
      this.schemaJson = JSON.stringify(parsed, null, 2);
    } catch (e: any) {
      this.toast.error('JSON Error', e.message || 'Cannot format invalid JSON.');
    }
  }

  checkCompatibility(): void {
    if (!this.schema) return;
    const currentActive = this.schema.activeVersion || this.schema.versions[0];
    const oldJson = currentActive ? currentActive.schemaJson : '';

    this.isCheckingCompat.set(true);
    this.schemaService.checkCompatibility({
      oldSchemaJson: oldJson,
      newSchemaJson: this.schemaJson,
      mode: this.schema.compatibilityMode
    }).subscribe({
      next: (res) => {
        this.isCheckingCompat.set(false);
        this.compatibilityResult.set(res);
      },
      error: (err) => {
        this.isCheckingCompat.set(false);
        const msg = err.error?.detail || err.error?.title || 'Compatibility check failed.';
        this.toast.error('Check Error', msg);
      }
    });
  }

  onClose(): void {
    this.closed.emit();
  }

  submit(): void {
    if (!this.schema) return;
    if (!this.version.trim()) {
      this.toast.error('Validation Error', 'Version string is required.');
      return;
    }
    try {
      JSON.parse(this.schemaJson);
    } catch {
      this.toast.error('Validation Error', 'Schema must be valid JSON.');
      return;
    }

    this.isSubmitting.set(true);
    const req: CreateSchemaVersionRequest = {
      version: this.version.trim(),
      schemaJson: this.schemaJson,
      description: this.description.trim() || undefined,
      setActive: this.setActive,
      forceOverrideCompatibility: this.forceOverride
    };

    this.schemaService.createVersion(this.schema.id, req).subscribe({
      next: (v) => {
        this.isSubmitting.set(false);
        this.toast.success('Version Created', `Version ${v.version} released for ${this.schema!.name}.`);
        this.versionCreated.emit();
        this.onClose();
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const msg = err.error?.detail || err.error?.title || 'Failed to create schema version.';
        this.toast.error('Version Release Failed', msg);
      }
    });
  }
}
