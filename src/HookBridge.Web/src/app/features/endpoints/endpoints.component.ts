import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EndpointService, CreateEndpointRequest, UpdateEndpointRequest } from '../../core/services/endpoint.service';
import { ToastService } from '../../shared/components/ui/toast/toast.service';
import {
  ButtonComponent,
  StatusBadgeComponent,
  SkeletonLoaderComponent,
  ModalComponent,
  SearchFilterBarComponent,
  EmptyStateComponent
} from '../../shared/components';
import {
  Endpoint,
  EndpointStatus,
  WebhookSecret,
  Application
} from '../../shared/models/control-plane.models';

@Component({
  selector: 'app-endpoints',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DatePipe,
    ButtonComponent,
    StatusBadgeComponent,
    SkeletonLoaderComponent,
    ModalComponent,
    SearchFilterBarComponent,
    EmptyStateComponent
  ],
  template: `
    <div class="space-y-6 pb-12">
      <!-- Top Title & Action Header -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 class="text-xl font-bold tracking-tight text-white">Webhook Endpoints</h1>
          <p class="text-xs text-surface-400 mt-1">
            Configure destination URLs, cryptographic signing secrets, dual-key rotation and event subscriptions.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <app-button
            variant="secondary"
            size="sm"
            [loading]="isLoading()"
            (clicked)="loadEndpoints()">
            <svg slot="icon-left" class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
            </svg>
            Refresh
          </app-button>

          <app-button
            variant="primary"
            size="sm"
            (clicked)="openCreateModal()">
            <svg slot="icon-left" class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
            </svg>
            Register Endpoint
          </app-button>
        </div>
      </div>

      <!-- Search & Status Filter Bar -->
      <app-search-filter-bar
        searchPlaceholder="Filter endpoints by URL, description or pattern..."
        [filters]="statusFilterConfig"
        [(searchQuery)]="searchQuery"
        [(activeFilters)]="activeFilters"
        (refresh)="loadEndpoints()">
      </app-search-filter-bar>

      <!-- Endpoints List -->
      <div class="space-y-4">
        @if (isLoading()) {
          <div class="space-y-3">
            @for (i of [1, 2, 3]; track i) {
              <div class="p-5 rounded-xl bg-surface-900/80 border border-surface-800 space-y-3">
                <div class="flex justify-between items-center">
                  <app-skeleton-loader customClass="h-5 w-1/3"></app-skeleton-loader>
                  <app-skeleton-loader customClass="h-5 w-20"></app-skeleton-loader>
                </div>
                <app-skeleton-loader customClass="h-4 w-1/2"></app-skeleton-loader>
                <div class="flex gap-2 pt-2">
                  <app-skeleton-loader customClass="h-6 w-16"></app-skeleton-loader>
                  <app-skeleton-loader customClass="h-6 w-24"></app-skeleton-loader>
                </div>
              </div>
            }
          </div>
        } @else if (filteredEndpoints().length === 0) {
          <div class="bg-surface-900/80 border border-surface-800 rounded-xl">
            <app-empty-state
              title="No Webhook Endpoints Found"
              description="Register an HTTPS destination endpoint to start delivering webhooks reliably with HMAC signatures.">
              <div slot="actions">
                <app-button variant="primary" size="sm" (clicked)="openCreateModal()">
                  Register First Endpoint
                </app-button>
              </div>
            </app-empty-state>
          </div>
        } @else {
          <div class="grid grid-cols-1 gap-4">
            @for (ep of filteredEndpoints(); track ep.id) {
              <div class="bg-surface-900/80 border border-surface-800 hover:border-surface-700/80 rounded-xl p-5 shadow-xs transition-all flex flex-col justify-between gap-4">
                <!-- Top Row: URL, Status & Actions -->
                <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
                  <div class="min-w-0 flex-1">
                    <div class="flex items-center gap-2.5 flex-wrap">
                      <app-status-badge [status]="ep.status"></app-status-badge>
                      <span class="text-sm font-semibold font-mono text-white select-all break-all">
                        {{ ep.targetUrl }}
                      </span>
                    </div>

                    @if (ep.description) {
                      <p class="text-xs text-surface-400 mt-1">{{ ep.description }}</p>
                    }
                  </div>

                  <!-- Action Buttons Toolbar -->
                  <div class="flex items-center gap-2 shrink-0">
                    <!-- Manage Secrets Button -->
                    <app-button
                      variant="secondary"
                      size="sm"
                      (clicked)="openSecretsModal(ep)">
                      <svg slot="icon-left" class="w-3.5 h-3.5 text-brand-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z"/>
                      </svg>
                      Secrets & Keys
                    </app-button>

                    <!-- Edit Button -->
                    <app-button
                      variant="outline"
                      size="sm"
                      (clicked)="openEditModal(ep)">
                      Edit
                    </app-button>

                    <!-- Toggle Status Button -->
                    <app-button
                      [variant]="ep.status === 'Active' ? 'ghost' : 'outline'"
                      size="sm"
                      (clicked)="toggleStatus(ep)">
                      {{ ep.status === 'Active' ? 'Pause' : 'Activate' }}
                    </app-button>

                    <!-- Delete Button -->
                    <button
                      type="button"
                      (click)="openDeleteModal(ep)"
                      title="Delete Endpoint"
                      class="p-1.5 text-surface-400 hover:text-rose-400 hover:bg-rose-950/40 rounded-lg transition-colors">
                      <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/>
                      </svg>
                    </button>
                  </div>
                </div>

                <!-- Middle: Subscribed Event Patterns -->
                <div class="pt-3 border-t border-surface-800/60 flex flex-wrap items-center gap-2">
                  <span class="text-[11px] font-medium text-surface-400">Subscriptions:</span>
                  @if (ep.subscribedEvents && ep.subscribedEvents.length > 0) {
                    @for (pattern of ep.subscribedEvents; track pattern) {
                      <span class="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-mono bg-brand-950/80 border border-brand-800/60 text-brand-300">
                        {{ pattern }}
                      </span>
                    }
                  } @else {
                    <span class="text-[11px] font-mono text-surface-500">All events (*)</span>
                  }
                </div>

                <!-- Bottom Metadata Row -->
                <div class="pt-2 border-t border-surface-800/40 flex flex-wrap items-center justify-between gap-4 text-xs font-mono text-surface-400">
                  <div class="flex items-center gap-4">
                    <span>Rate Limit: <strong class="text-surface-200">{{ ep.rateLimitPerMinute }}/min</strong></span>
                    <span>&bull;</span>
                    <span>Timeout: <strong class="text-surface-200">{{ ep.timeoutSeconds }}s</strong></span>
                    @if (ep.activeSecretPrefix) {
                      <span>&bull;</span>
                      <span>Active Key: <strong class="text-brand-300">{{ ep.activeSecretPrefix }} (v{{ ep.activeSecretVersion ?? 1 }})</strong></span>
                    }
                  </div>

                  <span class="text-[11px] font-sans text-surface-500">
                    Created {{ ep.createdAt | date:'MMM d, y, HH:mm' }}
                  </span>
                </div>
              </div>
            }
          </div>
        }
      </div>

      <!-- MODAL 1: Create Endpoint Modal -->
      <app-modal
        [isOpen]="isCreateOpen()"
        title="Register Destination Endpoint"
        size="lg"
        (closed)="closeCreateModal()">
        <form (ngSubmit)="submitCreate()" class="space-y-4 font-sans">
          <!-- Application Select -->
          <div>
            <label class="block text-xs font-medium text-surface-300 mb-1">Target Application</label>
            @if (applications().length > 0) {
              <select
                [(ngModel)]="createForm.applicationId"
                name="applicationId"
                required
                class="w-full bg-surface-950 border border-surface-800 rounded-lg px-3 py-2 text-xs text-white focus:border-brand-500 focus:outline-none">
                @for (app of applications(); track app.id) {
                  <option [value]="app.id">{{ app.name }}</option>
                }
              </select>
            } @else {
              <div class="text-xs text-surface-400 bg-surface-950 p-2.5 rounded-lg border border-surface-800">
                Default Application will be automatically attached.
              </div>
            }
          </div>

          <!-- Target URL -->
          <div>
            <label class="block text-xs font-medium text-surface-300 mb-1">
              Destination URL <span class="text-rose-400">*</span>
            </label>
            <input
              type="url"
              [(ngModel)]="createForm.targetUrl"
              name="targetUrl"
              required
              placeholder="https://api.yourdomain.com/webhooks/receiver"
              class="w-full bg-surface-950 border border-surface-800 rounded-lg px-3 py-2 text-xs text-white placeholder-surface-500 font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
            <p class="text-[11px] text-surface-500 mt-1">
              Must be a valid HTTPS URL. Localhost and internal RFC1918 IPs are blocked by SSRF Guard in production.
            </p>
          </div>

          <!-- Event Subscriptions -->
          <div>
            <label class="block text-xs font-medium text-surface-300 mb-1">
              Subscribed Event Patterns (comma separated)
            </label>
            <input
              type="text"
              [(ngModel)]="createForm.eventPatternsText"
              name="eventPatterns"
              placeholder="e.g. order.*, payment.succeeded, invoice.created"
              class="w-full bg-surface-950 border border-surface-800 rounded-lg px-3 py-2 text-xs text-white placeholder-surface-500 font-mono focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500" />
            <p class="text-[11px] text-surface-500 mt-1">
              Use wildcards (e.g. <code class="text-brand-300">order.*</code>) or exact matches. Leave empty to receive all events (<code class="text-brand-300">*</code>).
            </p>
          </div>

          <!-- Description -->
          <div>
            <label class="block text-xs font-medium text-surface-300 mb-1">Description (Optional)</label>
            <input
              type="text"
              [(ngModel)]="createForm.description"
              name="description"
              placeholder="Primary billing webhook receiver"
              class="w-full bg-surface-950 border border-surface-800 rounded-lg px-3 py-2 text-xs text-white placeholder-surface-500 focus:border-brand-500 focus:outline-none" />
          </div>

          <!-- Rate Limit and Timeout Grid -->
          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="block text-xs font-medium text-surface-300 mb-1">Rate Limit (req/min)</label>
              <input
                type="number"
                [(ngModel)]="createForm.rateLimitPerMinute"
                name="rateLimit"
                min="10"
                max="10000"
                class="w-full bg-surface-950 border border-surface-800 rounded-lg px-3 py-2 text-xs text-white font-mono focus:border-brand-500 focus:outline-none" />
            </div>

            <div>
              <label class="block text-xs font-medium text-surface-300 mb-1">Timeout (seconds)</label>
              <input
                type="number"
                [(ngModel)]="createForm.timeoutSeconds"
                name="timeout"
                min="1"
                max="60"
                class="w-full bg-surface-950 border border-surface-800 rounded-lg px-3 py-2 text-xs text-white font-mono focus:border-brand-500 focus:outline-none" />
            </div>
          </div>

          <div slot="footer" class="flex items-center justify-end gap-3 pt-3">
            <app-button variant="ghost" size="sm" (clicked)="closeCreateModal()">
              Cancel
            </app-button>
            <app-button variant="primary" size="sm" type="submit" [loading]="isSubmitting()">
              Register Endpoint
            </app-button>
          </div>
        </form>
      </app-modal>

      <!-- MODAL 2: Secret Revealed Modal (One-Time Token Display) -->
      <app-modal
        [isOpen]="isSecretRevealedOpen()"
        title="Signing Secret Generated"
        size="md"
        [closeOnBackdrop]="false"
        (closed)="closeSecretRevealedModal()">
        <div class="space-y-4 font-sans">
          <div class="p-3 bg-amber-950/40 border border-amber-800/80 rounded-xl text-amber-300 text-xs flex items-start gap-2.5">
            <svg class="w-5 h-5 shrink-0 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>
            </svg>
            <div>
              <strong class="font-semibold block">Copy your HMAC signing secret now!</strong>
              For cryptographic safety, this key is encrypted in our database and will <strong>never</strong> be displayed again.
            </div>
          </div>

          <div>
            <label class="block text-xs font-semibold text-surface-300 mb-1">Webhook Secret Key</label>
            <div class="relative">
              <input
                type="text"
                readonly
                [value]="revealedSecret()"
                class="w-full bg-surface-950 border border-brand-500/50 rounded-lg px-3 py-2.5 pr-20 text-xs text-brand-300 font-mono select-all focus:outline-none" />
              <button
                type="button"
                (click)="copyRevealedSecret()"
                class="absolute right-1.5 top-1.5 bottom-1.5 px-3 rounded bg-brand-600 hover:bg-brand-500 text-white text-xs font-medium transition-colors">
                {{ isCopied() ? 'Copied!' : 'Copy' }}
              </button>
            </div>
          </div>

          <div slot="footer" class="flex justify-end">
            <app-button variant="primary" size="sm" (clicked)="closeSecretRevealedModal()">
              I Have Saved My Secret
            </app-button>
          </div>
        </div>
      </app-modal>

      <!-- MODAL 3: Secret Rotation & Key Management Modal -->
      <app-modal
        [isOpen]="isSecretsModalOpen()"
        title="Signing Key Rotation"
        size="lg"
        (closed)="closeSecretsModal()">
        <div class="space-y-5 font-sans">
          <div class="p-3.5 bg-surface-950 rounded-xl border border-surface-800 text-xs text-surface-300">
            <h4 class="font-semibold text-white mb-1">Zero-Downtime Dual Secret Rotation</h4>
            <p class="text-surface-400 text-[11px] leading-relaxed">
              When rotating, HookBridge generates a new active key and signs with both active and previous keys, providing a seamless tolerance window for your destination server to update its verification code.
            </p>
          </div>

          <!-- Secrets Table -->
          <div>
            <div class="flex items-center justify-between mb-2">
              <span class="text-xs font-semibold text-surface-300">Key History</span>
              <app-button
                variant="primary"
                size="sm"
                [loading]="isRotating()"
                (clicked)="rotateSecret()">
                <svg slot="icon-left" class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
                </svg>
                Rotate to New Key
              </app-button>
            </div>

            <div class="bg-surface-950 border border-surface-800 rounded-xl overflow-hidden text-xs">
              <table class="w-full text-left border-collapse">
                <thead>
                  <tr class="border-b border-surface-800 bg-surface-900/60 text-surface-400 uppercase text-[10px] font-semibold">
                    <th class="p-3">Version</th>
                    <th class="p-3">Prefix</th>
                    <th class="p-3">Status</th>
                    <th class="p-3">Created</th>
                    <th class="p-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-surface-800/60 font-mono">
                  @for (sec of endpointSecrets(); track sec.id) {
                    <tr>
                      <td class="p-3 text-white font-bold">v{{ sec.version }}</td>
                      <td class="p-3 text-brand-300">{{ sec.keyPrefix }}...</td>
                      <td class="p-3 font-sans">
                        <app-status-badge [status]="sec.status"></app-status-badge>
                      </td>
                      <td class="p-3 text-surface-400 font-sans">{{ sec.createdAt | date:'MMM d, y, HH:mm' }}</td>
                      <td class="p-3 text-right">
                        @if (sec.status !== 'Revoked') {
                          <button
                            (click)="revokeSecret(sec.id)"
                            type="button"
                            class="text-xs text-rose-400 hover:text-rose-300 hover:underline">
                            Revoke
                          </button>
                        } @else {
                          <span class="text-surface-600">Revoked</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>

          <div slot="footer" class="flex justify-end">
            <app-button variant="secondary" size="sm" (clicked)="closeSecretsModal()">
              Close
            </app-button>
          </div>
        </div>
      </app-modal>

      <!-- MODAL 4: Edit Endpoint Modal -->
      <app-modal
        [isOpen]="isEditOpen()"
        title="Edit Webhook Endpoint"
        size="md"
        (closed)="closeEditModal()">
        <form (ngSubmit)="submitEdit()" class="space-y-4 font-sans">
          <div>
            <label class="block text-xs font-medium text-surface-300 mb-1">Target URL</label>
            <input
              type="url"
              [(ngModel)]="editForm.targetUrl"
              name="targetUrl"
              required
              class="w-full bg-surface-950 border border-surface-800 rounded-lg px-3 py-2 text-xs text-white font-mono focus:border-brand-500 focus:outline-none" />
          </div>

          <div>
            <label class="block text-xs font-medium text-surface-300 mb-1">Description</label>
            <input
              type="text"
              [(ngModel)]="editForm.description"
              name="description"
              class="w-full bg-surface-950 border border-surface-800 rounded-lg px-3 py-2 text-xs text-white focus:border-brand-500 focus:outline-none" />
          </div>

          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="block text-xs font-medium text-surface-300 mb-1">Rate Limit (req/min)</label>
              <input
                type="number"
                [(ngModel)]="editForm.rateLimitPerMinute"
                name="rateLimit"
                min="10"
                class="w-full bg-surface-950 border border-surface-800 rounded-lg px-3 py-2 text-xs text-white font-mono focus:border-brand-500 focus:outline-none" />
            </div>

            <div>
              <label class="block text-xs font-medium text-surface-300 mb-1">Timeout (seconds)</label>
              <input
                type="number"
                [(ngModel)]="editForm.timeoutSeconds"
                name="timeout"
                min="1"
                max="60"
                class="w-full bg-surface-950 border border-surface-800 rounded-lg px-3 py-2 text-xs text-white font-mono focus:border-brand-500 focus:outline-none" />
            </div>
          </div>

          <div slot="footer" class="flex items-center justify-end gap-3 pt-3">
            <app-button variant="ghost" size="sm" (clicked)="closeEditModal()">Cancel</app-button>
            <app-button variant="primary" size="sm" type="submit" [loading]="isSubmitting()">Save Changes</app-button>
          </div>
        </form>
      </app-modal>

      <!-- MODAL 5: Delete Confirmation Modal -->
      <app-modal
        [isOpen]="isDeleteOpen()"
        title="Delete Endpoint Confirmation"
        size="sm"
        (closed)="closeDeleteModal()">
        <div class="space-y-3 font-sans text-xs text-surface-300">
          <p>
            Are you sure you want to delete this endpoint?
          </p>
          <div class="p-2.5 rounded-lg bg-surface-950 border border-surface-800 font-mono text-[11px] text-white break-all">
            {{ selectedEndpoint()?.targetUrl }}
          </div>
          <p class="text-rose-400">
            This will permanently remove all subscriptions and signing secrets. Any in-flight webhooks scheduled for this destination will be dropped.
          </p>

          <div slot="footer" class="flex items-center justify-end gap-3 pt-2">
            <app-button variant="ghost" size="sm" (clicked)="closeDeleteModal()">Cancel</app-button>
            <app-button variant="danger" size="sm" [loading]="isSubmitting()" (clicked)="confirmDelete()">
              Delete Endpoint
            </app-button>
          </div>
        </div>
      </app-modal>
    </div>
  `
})
export class EndpointsComponent implements OnInit {
  private readonly endpointService = inject(EndpointService);
  private readonly toast = inject(ToastService);

  readonly endpoints = signal<Endpoint[]>([]);
  readonly applications = signal<Application[]>([]);
  readonly isLoading = signal<boolean>(true);
  readonly isSubmitting = signal<boolean>(false);
  readonly isRotating = signal<boolean>(false);

  readonly searchQuery = signal<string>('');
  readonly activeFilters = signal<Record<string, string>>({});

  // Modals state
  readonly isCreateOpen = signal<boolean>(false);
  readonly isEditOpen = signal<boolean>(false);
  readonly isDeleteOpen = signal<boolean>(false);
  readonly isSecretsModalOpen = signal<boolean>(false);
  readonly isSecretRevealedOpen = signal<boolean>(false);

  readonly selectedEndpoint = signal<Endpoint | null>(null);
  readonly endpointSecrets = signal<WebhookSecret[]>([]);
  readonly revealedSecret = signal<string>('');
  readonly isCopied = signal<boolean>(false);

  readonly statusFilterConfig = [
    {
      id: 'status',
      label: 'Status',
      options: [
        { label: 'Active', value: 'Active' },
        { label: 'Paused', value: 'Paused' },
        { label: 'Disabled', value: 'Disabled' }
      ]
    }
  ];

  createForm = {
    applicationId: '',
    targetUrl: '',
    description: '',
    eventPatternsText: '',
    rateLimitPerMinute: 600,
    timeoutSeconds: 10
  };

  editForm = {
    targetUrl: '',
    description: '',
    rateLimitPerMinute: 600,
    timeoutSeconds: 10
  };

  readonly filteredEndpoints = computed(() => {
    const list = this.endpoints();
    const query = this.searchQuery().toLowerCase().trim();
    const statusFilter = this.activeFilters()['status'];

    return list.filter(ep => {
      if (statusFilter && ep.status !== statusFilter) {
        return false;
      }
      if (query) {
        const matchesUrl = ep.targetUrl.toLowerCase().includes(query);
        const matchesDesc = (ep.description ?? '').toLowerCase().includes(query);
        const matchesPattern = ep.subscribedEvents?.some(p => p.toLowerCase().includes(query));
        if (!matchesUrl && !matchesDesc && !matchesPattern) {
          return false;
        }
      }
      return true;
    });
  });

  ngOnInit(): void {
    this.loadEndpoints();
    this.loadApplications();
  }

  loadEndpoints(): void {
    this.isLoading.set(true);
    this.endpointService.getEndpoints().subscribe({
      next: (res) => {
        this.endpoints.set(res || []);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.toast.error('Failed to load webhook endpoints');
      }
    });
  }

  loadApplications(): void {
    this.endpointService.getApplications().subscribe({
      next: (res) => {
        this.applications.set(res || []);
        if (res && res.length > 0 && !this.createForm.applicationId) {
          this.createForm.applicationId = res[0].id;
        }
      },
      error: () => {}
    });
  }

  openCreateModal(): void {
    if (this.applications().length > 0) {
      this.createForm.applicationId = this.applications()[0].id;
    }
    this.createForm.targetUrl = '';
    this.createForm.description = '';
    this.createForm.eventPatternsText = '';
    this.createForm.rateLimitPerMinute = 600;
    this.createForm.timeoutSeconds = 10;
    this.isCreateOpen.set(true);
  }

  closeCreateModal(): void {
    this.isCreateOpen.set(false);
  }

  submitCreate(): void {
    if (!this.createForm.targetUrl) {
      this.toast.error('Destination URL is required');
      return;
    }

    const patterns = this.createForm.eventPatternsText
      .split(',')
      .map(p => p.trim())
      .filter(p => p.length > 0);

    const payload: CreateEndpointRequest = {
      applicationId: this.createForm.applicationId,
      targetUrl: this.createForm.targetUrl,
      description: this.createForm.description || null,
      rateLimitPerMinute: this.createForm.rateLimitPerMinute,
      timeoutSeconds: this.createForm.timeoutSeconds,
      subscribedEvents: patterns.length > 0 ? patterns : ['*']
    };

    this.isSubmitting.set(true);
    this.endpointService.createEndpoint(payload).subscribe({
      next: (created) => {
        this.isSubmitting.set(false);
        this.closeCreateModal();
        this.loadEndpoints();
        this.toast.success('Endpoint registered successfully');

        // Reveal the newly generated raw HMAC secret
        if (created.initialSecret) {
          this.revealedSecret.set(created.initialSecret);
          this.isSecretRevealedOpen.set(true);
        }
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.toast.error(err?.error?.detail ?? 'Failed to register endpoint');
      }
    });
  }

  openSecretsModal(ep: Endpoint): void {
    this.selectedEndpoint.set(ep);
    this.isSecretsModalOpen.set(true);
    this.loadSecrets(ep.id);
  }

  closeSecretsModal(): void {
    this.isSecretsModalOpen.set(false);
    this.selectedEndpoint.set(null);
  }

  loadSecrets(endpointId: string): void {
    this.endpointService.getSecrets(endpointId).subscribe({
      next: (secrets) => {
        this.endpointSecrets.set(secrets || []);
      },
      error: () => {
        this.toast.error('Failed to load signing secrets');
      }
    });
  }

  rotateSecret(): void {
    const ep = this.selectedEndpoint();
    if (!ep) return;

    this.isRotating.set(true);
    this.endpointService.rotateSecret(ep.id).subscribe({
      next: (res) => {
        this.isRotating.set(false);
        this.toast.success('Signing secret rotated successfully');
        this.loadSecrets(ep.id);
        this.loadEndpoints();

        // Reveal the new raw secret
        this.revealedSecret.set(res.newSecret);
        this.isSecretRevealedOpen.set(true);
      },
      error: (err) => {
        this.isRotating.set(false);
        this.toast.error(err?.error?.detail ?? 'Failed to rotate signing secret');
      }
    });
  }

  revokeSecret(secretId: string): void {
    const ep = this.selectedEndpoint();
    if (!ep) return;

    this.endpointService.revokeSecret(ep.id, secretId).subscribe({
      next: () => {
        this.toast.success('Secret revoked');
        this.loadSecrets(ep.id);
      },
      error: (err) => {
        this.toast.error(err?.error?.detail ?? 'Failed to revoke secret');
      }
    });
  }

  copyRevealedSecret(): void {
    navigator.clipboard.writeText(this.revealedSecret()).then(() => {
      this.isCopied.set(true);
      setTimeout(() => this.isCopied.set(false), 2000);
    });
  }

  closeSecretRevealedModal(): void {
    this.isSecretRevealedOpen.set(false);
    this.revealedSecret.set('');
  }

  openEditModal(ep: Endpoint): void {
    this.selectedEndpoint.set(ep);
    this.editForm.targetUrl = ep.targetUrl;
    this.editForm.description = ep.description ?? '';
    this.editForm.rateLimitPerMinute = ep.rateLimitPerMinute;
    this.editForm.timeoutSeconds = ep.timeoutSeconds;
    this.isEditOpen.set(true);
  }

  closeEditModal(): void {
    this.isEditOpen.set(false);
    this.selectedEndpoint.set(null);
  }

  submitEdit(): void {
    const ep = this.selectedEndpoint();
    if (!ep) return;

    const payload: UpdateEndpointRequest = {
      targetUrl: this.editForm.targetUrl,
      description: this.editForm.description || null,
      rateLimitPerMinute: this.editForm.rateLimitPerMinute,
      timeoutSeconds: this.editForm.timeoutSeconds
    };

    this.isSubmitting.set(true);
    this.endpointService.updateEndpoint(ep.id, payload).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.closeEditModal();
        this.toast.success('Endpoint updated successfully');
        this.loadEndpoints();
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.toast.error(err?.error?.detail ?? 'Failed to update endpoint');
      }
    });
  }

  toggleStatus(ep: Endpoint): void {
    const newStatus: EndpointStatus = ep.status === 'Active' ? 'Paused' : 'Active';
    this.endpointService.updateStatus(ep.id, newStatus).subscribe({
      next: () => {
        this.toast.success(`Endpoint status updated to ${newStatus}`);
        this.loadEndpoints();
      },
      error: (err) => {
        this.toast.error(err?.error?.detail ?? 'Failed to toggle status');
      }
    });
  }

  openDeleteModal(ep: Endpoint): void {
    this.selectedEndpoint.set(ep);
    this.isDeleteOpen.set(true);
  }

  closeDeleteModal(): void {
    this.isDeleteOpen.set(false);
    this.selectedEndpoint.set(null);
  }

  confirmDelete(): void {
    const ep = this.selectedEndpoint();
    if (!ep) return;

    this.isSubmitting.set(true);
    this.endpointService.deleteEndpoint(ep.id).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.closeDeleteModal();
        this.toast.success('Endpoint deleted successfully');
        this.loadEndpoints();
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.toast.error(err?.error?.detail ?? 'Failed to delete endpoint');
      }
    });
  }
}
