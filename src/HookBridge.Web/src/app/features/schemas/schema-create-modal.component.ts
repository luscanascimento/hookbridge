import { Component, EventEmitter, Input, Output, inject, signal, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ModalComponent, ButtonComponent } from '../../shared/components';
import { EventSchemaService } from '../../core/services/event-schema.service';
import { ToastService } from '../../shared/components/ui/toast/toast.service';
import { SchemaCompatibilityMode, CreateEventSchemaRequest } from '../../core/models/event-schema.models';

const SCHEMA_TEMPLATES: Record<string, { eventType: string; name: string; desc: string; json: string }> = {
  'order.created': {
    eventType: 'order.created',
    name: 'Order Created',
    desc: 'Emitted immediately after a customer successfully places an order.',
    json: JSON.stringify({
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "title": "OrderCreatedEvent",
      "type": "object",
      "required": ["orderId", "amount", "currency", "customer"],
      "properties": {
        "orderId": { "type": "string", "format": "uuid", "description": "Unique identifier for the order" },
        "amount": { "type": "number", "description": "Total order amount in minor or decimal units" },
        "currency": { "type": "string", "enum": ["USD", "EUR", "BRL", "GBP"], "description": "ISO 4217 currency code" },
        "status": { "type": "string", "enum": ["pending", "confirmed", "processing"], "description": "Current order fulfillment status" },
        "customer": {
          "type": "object",
          "required": ["id", "email"],
          "properties": {
            "id": { "type": "string", "description": "Customer unique ID" },
            "email": { "type": "string", "format": "email", "description": "Customer contact email" },
            "name": { "type": "string", "description": "Full customer name" }
          }
        },
        "items": {
          "type": "array",
          "items": {
            "type": "object",
            "required": ["sku", "quantity", "unitPrice"],
            "properties": {
              "sku": { "type": "string" },
              "quantity": { "type": "integer" },
              "unitPrice": { "type": "number" }
            }
          }
        }
      }
    }, null, 2)
  },
  'invoice.paid': {
    eventType: 'invoice.paid',
    name: 'Invoice Paid',
    desc: 'Triggered when an invoice payment is settled.',
    json: JSON.stringify({
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "title": "InvoicePaidEvent",
      "type": "object",
      "required": ["invoiceId", "customerId", "amountPaid", "paidAt"],
      "properties": {
        "invoiceId": { "type": "string", "format": "uuid" },
        "customerId": { "type": "string" },
        "amountPaid": { "type": "number" },
        "currency": { "type": "string" },
        "paidAt": { "type": "string", "format": "date-time" },
        "paymentMethod": { "type": "string", "enum": ["credit_card", "pix", "wire_transfer", "crypto"] }
      }
    }, null, 2)
  },
  'user.registered': {
    eventType: 'user.registered',
    name: 'User Registered',
    desc: 'Triggered when a new user signs up.',
    json: JSON.stringify({
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "title": "UserRegisteredEvent",
      "type": "object",
      "required": ["userId", "email", "registeredAt"],
      "properties": {
        "userId": { "type": "string", "format": "uuid" },
        "email": { "type": "string", "format": "email" },
        "role": { "type": "string", "enum": ["admin", "developer", "viewer"] },
        "registeredAt": { "type": "string", "format": "date-time" }
      }
    }, null, 2)
  },
  'payment.disputed': {
    eventType: 'payment.disputed',
    name: 'Payment Disputed',
    desc: 'Emitted when a customer files a chargeback dispute.',
    json: JSON.stringify({
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "title": "PaymentDisputedEvent",
      "type": "object",
      "required": ["disputeId", "chargeId", "amount", "reason"],
      "properties": {
        "disputeId": { "type": "string", "format": "uuid" },
        "chargeId": { "type": "string" },
        "amount": { "type": "number" },
        "reason": { "type": "string", "enum": ["fraudulent", "unrecognized", "duplicate", "product_not_received"] },
        "evidenceDueBy": { "type": "string", "format": "date-time" }
      }
    }, null, 2)
  },
  'custom': {
    eventType: 'custom.event',
    name: 'Custom Event',
    desc: 'Custom payload schema definition.',
    json: JSON.stringify({
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "type": "object",
      "required": ["id", "timestamp"],
      "properties": {
        "id": { "type": "string", "format": "uuid" },
        "timestamp": { "type": "string", "format": "date-time" },
        "data": { "type": "object" }
      }
    }, null, 2)
  }
};

@Component({
  selector: 'app-schema-create-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, ModalComponent, ButtonComponent],
  template: `
    <app-modal [isOpen]="isOpen" (closed)="onClose()" title="Register Event Schema" size="xl">
      <div class="space-y-4">
        <!-- Preset Selector -->
        <div>
          <label class="block text-xs font-semibold text-surface-400 uppercase tracking-wider mb-1.5">
            Schema Preset Template
          </label>
          <div class="grid grid-cols-3 sm:grid-cols-5 gap-2">
            @for (key of templateKeys; track key) {
              <button type="button" (click)="applyTemplate(key)"
                      class="px-2.5 py-1.5 rounded text-xs font-mono border transition-all text-center"
                      [class.bg-brand-500-15]="selectedTemplate() === key"
                      [class.border-brand-500]="selectedTemplate() === key"
                      [class.text-brand-300]="selectedTemplate() === key"
                      [class.bg-surface-800]="selectedTemplate() !== key"
                      [class.border-surface-700]="selectedTemplate() !== key"
                      [class.text-surface-300]="selectedTemplate() !== key">
                {{ key }}
              </button>
            }
          </div>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <!-- Event Type -->
          <div>
            <label class="block text-xs font-medium text-surface-300 mb-1">
              Event Type <span class="text-rose-400">*</span>
            </label>
            <input type="text" [(ngModel)]="eventType" placeholder="e.g. order.created, invoice.paid"
                   class="w-full bg-surface-950 border border-surface-700 rounded-lg px-3 py-2 text-sm text-surface-100 font-mono focus:outline-none focus:border-brand-500" />
            <p class="text-[11px] text-surface-500 mt-1">Unique event identifier for subscription matching.</p>
          </div>

          <!-- Human Name -->
          <div>
            <label class="block text-xs font-medium text-surface-300 mb-1">
              Schema Name <span class="text-rose-400">*</span>
            </label>
            <input type="text" [(ngModel)]="name" placeholder="e.g. Order Created"
                   class="w-full bg-surface-950 border border-surface-700 rounded-lg px-3 py-2 text-sm text-surface-100 focus:outline-none focus:border-brand-500" />
          </div>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <!-- Compatibility Mode -->
          <div>
            <label class="block text-xs font-medium text-surface-300 mb-1">
              Evolution Compatibility Mode <span class="text-rose-400">*</span>
            </label>
            <select [(ngModel)]="compatibilityMode"
                    class="w-full bg-surface-950 border border-surface-700 rounded-lg px-3 py-2 text-sm text-surface-100 focus:outline-none focus:border-brand-500">
              <option value="Backward">Backward (Default — New version can read old data)</option>
              <option value="Forward">Forward (Old consumers can read new version data)</option>
              <option value="Full">Full (Bidirectional — Backward + Forward)</option>
              <option value="None">None (No compatibility enforcement)</option>
            </select>
          </div>

          <!-- Initial Version -->
          <div>
            <label class="block text-xs font-medium text-surface-300 mb-1">
              Initial Version
            </label>
            <input type="text" [(ngModel)]="version" placeholder="1.0.0"
                   class="w-full bg-surface-950 border border-surface-700 rounded-lg px-3 py-2 text-sm text-surface-100 font-mono focus:outline-none focus:border-brand-500" />
          </div>
        </div>

        <!-- Description -->
        <div>
          <label class="block text-xs font-medium text-surface-300 mb-1">
            Description
          </label>
          <input type="text" [(ngModel)]="description" placeholder="Brief explanation of when this event is fired"
                 class="w-full bg-surface-950 border border-surface-700 rounded-lg px-3 py-2 text-sm text-surface-100 focus:outline-none focus:border-brand-500" />
        </div>

        <!-- JSON Schema Editor -->
        <div>
          <div class="flex items-center justify-between mb-1.5">
            <label class="text-xs font-medium text-surface-300">
              JSON Schema Specification (Draft 2020-12) <span class="text-rose-400">*</span>
            </label>
            <div class="flex items-center gap-2">
              @if (isJsonValid()) {
                <span class="text-[11px] font-mono text-emerald-400 bg-emerald-950/40 px-2 py-0.5 rounded border border-emerald-800/60">
                  ✓ Valid JSON
                </span>
              } @else {
                <span class="text-[11px] font-mono text-rose-400 bg-rose-950/40 px-2 py-0.5 rounded border border-rose-800/60">
                  ✕ Invalid JSON
                </span>
              }
              <button type="button" (click)="formatSchemaJson()"
                      class="text-xs text-brand-400 hover:text-brand-300 transition-colors">
                Format JSON
              </button>
            </div>
          </div>
          <textarea [(ngModel)]="schemaJson" (ngModelChange)="checkJsonValidity()"
                    rows="10"
                    placeholder="{ ... JSON Schema ... }"
                    class="w-full bg-surface-950 border border-surface-700 rounded-lg p-3 text-xs font-mono text-surface-200 focus:outline-none focus:border-brand-500 leading-relaxed"></textarea>
        </div>

        <!-- Modal Actions -->
        <div class="flex items-center justify-end gap-3 pt-4 border-t border-surface-800">
          <app-button variant="outline" (click)="onClose()">Cancel</app-button>
          <app-button variant="primary" [loading]="isSubmitting()" (click)="submit()">
            Register Schema
          </app-button>
        </div>
      </div>
    </app-modal>
  `
})
export class SchemaCreateModalComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly schemaService = inject(EventSchemaService);
  private readonly toast = inject(ToastService);

  @Input() isOpen = false;
  @Output() closed = new EventEmitter<void>();
  @Output() schemaCreated = new EventEmitter<void>();

  readonly templateKeys = Object.keys(SCHEMA_TEMPLATES);
  readonly selectedTemplate = signal<string>('order.created');
  readonly isSubmitting = signal<boolean>(false);
  readonly isJsonValid = signal<boolean>(true);

  eventType = 'order.created';
  name = 'Order Created';
  description = 'Emitted immediately after a customer successfully places an order.';
  compatibilityMode: SchemaCompatibilityMode = 'Backward';
  version = '1.0.0';
  schemaJson = SCHEMA_TEMPLATES['order.created'].json;

  applyTemplate(key: string): void {
    this.selectedTemplate.set(key);
    const tmpl = SCHEMA_TEMPLATES[key];
    if (tmpl) {
      this.eventType = tmpl.eventType;
      this.name = tmpl.name;
      this.description = tmpl.desc;
      this.schemaJson = tmpl.json;
      this.isJsonValid.set(true);
    }
  }

  checkJsonValidity(): void {
    try {
      JSON.parse(this.schemaJson);
      this.isJsonValid.set(true);
    } catch {
      this.isJsonValid.set(false);
    }
  }

  formatSchemaJson(): void {
    try {
      const parsed = JSON.parse(this.schemaJson);
      this.schemaJson = JSON.stringify(parsed, null, 2);
      this.isJsonValid.set(true);
    } catch (e: any) {
      this.toast.error('JSON Error', e.message || 'Cannot format invalid JSON.');
    }
  }

  onClose(): void {
    this.closed.emit();
  }

  submit(): void {
    if (!this.eventType.trim()) {
      this.toast.error('Validation Error', 'Event Type is required.');
      return;
    }
    if (!this.name.trim()) {
      this.toast.error('Validation Error', 'Schema Name is required.');
      return;
    }
    if (!this.isJsonValid()) {
      this.toast.error('Validation Error', 'Schema JSON must be valid JSON.');
      return;
    }

    this.isSubmitting.set(true);
    const req: CreateEventSchemaRequest = {
      eventType: this.eventType.trim(),
      name: this.name.trim(),
      description: this.description.trim() || undefined,
      compatibilityMode: this.compatibilityMode,
      schemaJson: this.schemaJson,
      version: this.version.trim() || '1.0.0',
      versionDescription: 'Initial schema release.'
    };

    this.schemaService.createSchema(req).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.toast.success('Schema Registered', `Event schema '${this.name}' created successfully.`);
        this.schemaCreated.emit();
        this.onClose();
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const msg = err.error?.detail || err.error?.title || 'Failed to register schema.';
        this.toast.error('Registration Failed', msg);
      }
    });
  }
}
