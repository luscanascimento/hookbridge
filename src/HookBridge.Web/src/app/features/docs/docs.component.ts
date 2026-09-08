import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { DocService } from '../../core/services/doc.service';
import { AuthService } from '../../core/auth/services/auth.service';
import { ToastService } from '../../shared/components/ui/toast/toast.service';
import {
  ApiReferenceResponse,
  DocEndpointDto,
  SdkRecipeDto
} from '../../core/models/doc.models';
import { SkeletonLoaderComponent } from '../../shared/components/ui/skeleton-loader.component';

@Component({
  selector: 'app-docs',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    SkeletonLoaderComponent
  ],
  template: `
    <div class="space-y-6 max-w-7xl mx-auto pb-16">
      <!-- Header Banner -->
      <div class="bg-gradient-to-r from-surface-900 via-surface-900/90 to-brand-950/40 p-6 rounded-2xl border border-surface-800 shadow-xl flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div class="flex items-center gap-2">
            <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-brand-500/20 text-brand-300 border border-brand-500/30">
              Developer Hub
            </span>
            <span class="text-xs text-surface-400 font-mono">OpenAPI 3.1 & SDKs</span>
          </div>
          <h1 class="text-2xl font-bold text-white mt-1">Documentation & API Reference</h1>
          <p class="text-xs text-surface-400 mt-0.5">
            Cryptographic HMAC-SHA256 verification recipes, code snippets in 5+ languages, and complete gateway API specifications.
          </p>
        </div>

        <!-- Global Language Selector & Search -->
        <div class="flex flex-wrap items-center gap-3">
          <div class="flex items-center bg-surface-950/80 p-1 rounded-xl border border-surface-800">
            @for (lang of supportedLanguages; track lang.id) {
              <button
                (click)="setLanguage(lang.id)"
                [class.bg-brand-600]="selectedLanguage() === lang.id"
                [class.text-white]="selectedLanguage() === lang.id"
                [class.text-surface-400]="selectedLanguage() !== lang.id"
                class="px-3 py-1.5 rounded-lg text-xs font-medium transition-all hover:text-white">
                {{ lang.label }}
              </button>
            }
          </div>
        </div>
      </div>

      <!-- Quick Search & API Context Bar -->
      <div class="flex flex-col sm:flex-row items-center justify-between gap-4 bg-surface-900/60 p-4 rounded-xl border border-surface-800 backdrop-blur-sm">
        <div class="relative w-full sm:w-96">
          <svg class="w-4 h-4 absolute left-3.5 top-3 text-surface-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
          </svg>
          <input
            type="text"
            [(ngModel)]="searchQuery"
            placeholder="Search guides, endpoints, SDK methods..."
            class="w-full bg-surface-950 text-surface-100 text-xs pl-10 pr-4 py-2 rounded-lg border border-surface-700 focus:outline-none focus:border-brand-500 transition-colors"
          />
        </div>

        <div class="flex items-center gap-3 w-full sm:w-auto justify-between sm:justify-end">
          <div class="text-xs text-surface-400 font-mono flex items-center gap-1.5 bg-surface-950 px-3 py-1.5 rounded-lg border border-surface-800">
            <span class="w-2 h-2 rounded-full bg-emerald-400"></span>
            Base URL: <span class="text-surface-200">{{ apiReference()?.baseUrl || 'https://api.hookbridge.io' }}</span>
          </div>

          <button
            (click)="copyBaseUrl()"
            class="p-1.5 text-xs text-surface-300 hover:text-white bg-surface-800 hover:bg-surface-700 rounded-lg border border-surface-700 transition-colors"
            title="Copy Base URL">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 002-2M8 5a2 2 0 012-2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10m0 0l3-3m-3 3l3 3"/>
            </svg>
          </button>
        </div>
      </div>

      <!-- Main Content Layout (Sidebar + Content) -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-8">
        <!-- Sticky Sidebar Navigation -->
        <div class="lg:col-span-3 space-y-6">
          <div class="sticky top-6 bg-surface-900/70 p-4 rounded-xl border border-surface-800 backdrop-blur space-y-5 max-h-[calc(100vh-6rem)] overflow-y-auto">
            <!-- Guides Section -->
            <div>
              <div class="text-[11px] font-bold text-surface-400 uppercase tracking-wider mb-2">Guides & Security</div>
              <ul class="space-y-1">
                @for (guide of apiReference()?.guides; track guide.id) {
                  <li>
                    <button
                      (click)="scrollToSection(guide.id)"
                      class="w-full text-left px-2.5 py-1.5 rounded-lg text-xs transition-colors hover:bg-surface-800 flex items-center justify-between text-surface-300 hover:text-white">
                      <span>{{ guide.title }}</span>
                      <span class="text-[10px] text-surface-500 font-mono">{{ guide.category }}</span>
                    </button>
                  </li>
                }
                <li>
                  <button
                    (click)="scrollToSection('signature-sandbox')"
                    class="w-full text-left px-2.5 py-1.5 rounded-lg text-xs transition-colors hover:bg-surface-800 flex items-center justify-between text-brand-300 font-medium bg-brand-950/30 border border-brand-800/40">
                    <span class="flex items-center gap-1.5">
                      <svg class="w-3.5 h-3.5 text-brand-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19.428 15.428a2 2 0 00-1.022-.547l-2.387-.477a6 6 0 00-3.86.517l-.318.158a6 6 0 01-3.86.517L6.05 15.21a2 2 0 00-1.806.547M8 4h8l-1 1v5.172a2 2 0 00.586 1.414l5 5c1.26 1.26.367 3.414-1.415 3.414H4.828c-1.782 0-2.674-2.154-1.414-3.414l5-5A2 2 0 009 10.172V5L8 4z"/>
                      </svg>
                      Signature Sandbox
                    </span>
                    <span class="text-[9px] bg-brand-500/20 text-brand-300 px-1.5 py-0.5 rounded">Tester</span>
                  </button>
                </li>
                <li>
                  <button
                    (click)="scrollToSection('sdk-recipes')"
                    class="w-full text-left px-2.5 py-1.5 rounded-lg text-xs transition-colors hover:bg-surface-800 flex items-center justify-between text-surface-300 hover:text-white">
                    <span>SDK Recipes</span>
                    <span class="text-[10px] text-surface-500 font-mono">5 Langs</span>
                  </button>
                </li>
              </ul>
            </div>

            <!-- API Endpoints Section -->
            <div>
              <div class="text-[11px] font-bold text-surface-400 uppercase tracking-wider mb-2">REST API Reference</div>
              <div class="space-y-3">
                @for (group of groupedEndpoints(); track group.category) {
                  <div>
                    <div class="text-[10px] font-semibold text-surface-400 px-2 py-0.5">{{ group.category }}</div>
                    <ul class="space-y-1 mt-1">
                      @for (ep of group.endpoints; track ep.id) {
                        <li>
                          <button
                            (click)="scrollToSection(ep.id)"
                            class="w-full text-left px-2 py-1 rounded text-[11px] transition-colors hover:bg-surface-800 flex items-center gap-2 text-surface-300 hover:text-white group">
                            <span [ngClass]="getMethodBadgeClass(ep.method)" class="font-mono text-[9px] font-bold px-1.5 py-0.2 rounded shrink-0">
                              {{ ep.method }}
                            </span>
                            <span class="truncate">{{ ep.summary }}</span>
                          </button>
                        </li>
                      }
                    </ul>
                  </div>
                }
              </div>
            </div>
          </div>
        </div>

        <!-- Documentation Main Content Area -->
        <div class="lg:col-span-9 space-y-12">
          @if (loading()) {
            <div class="space-y-6">
              <app-skeleton-loader customClass="h-48 w-full rounded-2xl"></app-skeleton-loader>
              <app-skeleton-loader customClass="h-48 w-full rounded-2xl"></app-skeleton-loader>
            </div>
          } @else {
            <!-- Guides Section -->
            @for (guide of filteredGuides(); track guide.id) {
              <section [id]="guide.id" class="bg-surface-900/80 rounded-2xl border border-surface-800 p-6 shadow-xl space-y-4">
                <div class="flex items-center justify-between border-b border-surface-800 pb-3">
                  <div>
                    <span class="text-[10px] font-semibold uppercase tracking-wider text-brand-400 bg-brand-950/50 px-2 py-0.5 rounded border border-brand-800/40">
                      {{ guide.category }}
                    </span>
                    <h2 class="text-lg font-bold text-white mt-1">{{ guide.title }}</h2>
                  </div>
                </div>

                <p class="text-xs text-surface-300 leading-relaxed">{{ guide.summary }}</p>

                <!-- Markdown Formatted Text Block -->
                <div class="bg-surface-950/60 p-4 rounded-xl border border-surface-800/80 text-xs text-surface-300 space-y-3 leading-relaxed font-sans">
                  <div [innerHTML]="renderMarkdown(guide.contentMarkdown)"></div>
                </div>

                <!-- Code Snippet for Guide -->
                @if (guide.codeSnippets && (guide.codeSnippets[selectedLanguage()] || guide.codeSnippets['typescript'] || guide.codeSnippets['curl'])) {
                  <div class="space-y-2">
                    <div class="flex items-center justify-between text-xs text-surface-400">
                      <span class="font-mono text-[11px]">Integration Example ({{ selectedLanguage().toUpperCase() }})</span>
                      <button
                        (click)="copyText(getActiveSnippet(guide.codeSnippets), 'Code snippet')"
                        class="flex items-center gap-1 text-[11px] text-brand-400 hover:text-brand-300 transition-colors">
                        <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 002-2M8 5a2 2 0 012-2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10m0 0l3-3m-3 3l3 3"/>
                        </svg>
                        Copy Snippet
                      </button>
                    </div>
                    <pre class="bg-surface-950 p-4 rounded-xl border border-surface-800 text-xs font-mono text-brand-200 overflow-x-auto"><code>{{ getActiveSnippet(guide.codeSnippets) }}</code></pre>
                  </div>
                }
              </section>
            }

            <!-- Webhook Signature Verification Sandbox / Tester -->
            <section id="signature-sandbox" class="bg-gradient-to-b from-surface-900 to-surface-950 rounded-2xl border border-brand-500/30 p-6 shadow-2xl space-y-6">
              <div class="flex items-center justify-between border-b border-surface-800 pb-4">
                <div class="flex items-center gap-3">
                  <div class="p-2.5 rounded-xl bg-brand-500/20 text-brand-300 border border-brand-500/30">
                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"/>
                    </svg>
                  </div>
                  <div>
                    <h2 class="text-base font-bold text-white">Interactive HMAC-SHA256 Signature Sandbox</h2>
                    <p class="text-xs text-surface-400">Test and verify the mathematical signature algorithm against sample payloads in real time.</p>
                  </div>
                </div>

                <div class="flex items-center gap-2">
                  <button
                    (click)="loadSandboxPreset('order')"
                    class="px-2.5 py-1 text-[11px] bg-surface-800 hover:bg-surface-700 text-surface-300 rounded border border-surface-700 transition-colors">
                    Preset: Order
                  </button>
                  <button
                    (click)="loadSandboxPreset('invoice')"
                    class="px-2.5 py-1 text-[11px] bg-surface-800 hover:bg-surface-700 text-surface-300 rounded border border-surface-700 transition-colors">
                    Preset: Invoice
                  </button>
                </div>
              </div>

              <!-- Sandbox Inputs -->
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div class="space-y-1.5">
                  <label class="text-[11px] font-semibold text-surface-300">Signing Secret (whsec_...)</label>
                  <input
                    type="text"
                    [(ngModel)]="sandboxSecret"
                    class="w-full bg-surface-950 text-surface-100 font-mono text-xs px-3 py-2 rounded-lg border border-surface-700 focus:outline-none focus:border-brand-500"
                  />
                </div>

                <div class="space-y-1.5">
                  <div class="flex items-center justify-between">
                    <label class="text-[11px] font-semibold text-surface-300">Timestamp (Unix Seconds)</label>
                    <button (click)="setSandboxTimestampToNow()" class="text-[10px] text-brand-400 hover:underline">
                      Set to Current Time
                    </button>
                  </div>
                  <input
                    type="number"
                    [(ngModel)]="sandboxTimestamp"
                    class="w-full bg-surface-950 text-surface-100 font-mono text-xs px-3 py-2 rounded-lg border border-surface-700 focus:outline-none focus:border-brand-500"
                  />
                </div>
              </div>

              <!-- Raw Payload JSON Input -->
              <div class="space-y-1.5">
                <label class="text-[11px] font-semibold text-surface-300">Raw Webhook Payload JSON</label>
                <textarea
                  [(ngModel)]="sandboxPayload"
                  rows="4"
                  class="w-full bg-surface-950 text-surface-100 font-mono text-xs p-3 rounded-lg border border-surface-700 focus:outline-none focus:border-brand-500 leading-relaxed"
                ></textarea>
              </div>

              <!-- Header Input & Actions -->
              <div class="space-y-1.5">
                <label class="text-[11px] font-semibold text-surface-300">Signature Header (X-HookBridge-Signature)</label>
                <input
                  type="text"
                  [(ngModel)]="sandboxHeader"
                  placeholder="t=1757270400,v1=6a8b9c...f4"
                  class="w-full bg-surface-950 text-surface-100 font-mono text-xs px-3 py-2 rounded-lg border border-surface-700 focus:outline-none focus:border-brand-500"
                />
              </div>

              <div class="flex flex-wrap items-center justify-between gap-3 pt-2">
                <button
                  (click)="generateSandboxSignature()"
                  class="px-4 py-2 bg-brand-600 hover:bg-brand-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-brand-600/30 transition-all flex items-center gap-2">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/>
                  </svg>
                  Generate Valid Signature
                </button>

                <button
                  (click)="verifySandboxSignature()"
                  [disabled]="sandboxVerifying()"
                  class="px-4 py-2 bg-surface-800 hover:bg-surface-700 text-surface-100 rounded-lg text-xs font-semibold border border-surface-700 transition-all flex items-center gap-2">
                  <svg class="w-4 h-4 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/>
                  </svg>
                  Verify Signature Online
                </button>
              </div>

              <!-- Verification Diagnostic Result Box -->
              @if (sandboxResult()) {
                <div [class.border-emerald-500/40]="sandboxResult()?.isValid"
                     [class.border-rose-500/40]="!sandboxResult()?.isValid"
                     [class.bg-emerald-950/20]="sandboxResult()?.isValid"
                     [class.bg-rose-950/20]="!sandboxResult()?.isValid"
                     class="p-4 rounded-xl border space-y-3 transition-all">
                  <div class="flex items-center justify-between">
                    <div class="flex items-center gap-2">
                      @if (sandboxResult()?.isValid) {
                        <span class="w-3 h-3 rounded-full bg-emerald-400 animate-pulse"></span>
                        <span class="text-xs font-bold text-emerald-300">SIGNATURE VALID & VERIFIED</span>
                      } @else {
                        <span class="w-3 h-3 rounded-full bg-rose-400"></span>
                        <span class="text-xs font-bold text-rose-300">SIGNATURE MISMATCH / INVALID</span>
                      }
                    </div>
                    <span class="text-[11px] text-surface-400 font-mono">
                      Clock skew: {{ sandboxResult()?.clockSkewSeconds || 0 }}s (max: 300s)
                    </span>
                  </div>

                  <div class="text-[11px] font-mono text-surface-300 bg-surface-950/80 p-2.5 rounded-lg border border-surface-800/80">
                    <span class="text-surface-500">Canonical String: </span>
                    <span class="text-brand-300">{{ sandboxTimestamp() }}.{{ sandboxPayload() }}</span>
                  </div>
                </div>
              }
            </section>

            <!-- SDK Recipes Section -->
            <section id="sdk-recipes" class="bg-surface-900/80 rounded-2xl border border-surface-800 p-6 shadow-xl space-y-6">
              <div class="flex items-center justify-between border-b border-surface-800 pb-4">
                <div>
                  <span class="text-[10px] font-semibold uppercase tracking-wider text-brand-400 bg-brand-950/50 px-2 py-0.5 rounded border border-brand-800/40">
                    SDKs & Libraries
                  </span>
                  <h2 class="text-lg font-bold text-white mt-1">Multi-Language SDK Integration Recipes</h2>
                  <p class="text-xs text-surface-400">Drop-in HMAC verification and event publishing functions tailored for each runtime.</p>
                </div>
              </div>

              <!-- SDK Language Tabs -->
              <div class="flex flex-wrap gap-2 border-b border-surface-800 pb-3">
                @for (recipe of apiReference()?.sdkRecipes; track recipe.language) {
                  <button
                    (click)="selectedSdkTab.set(recipe.language)"
                    [class.bg-brand-600]="selectedSdkTab() === recipe.language"
                    [class.text-white]="selectedSdkTab() === recipe.language"
                    [class.text-surface-400]="selectedSdkTab() !== recipe.language"
                    [class.bg-surface-950]="selectedSdkTab() !== recipe.language"
                    class="px-3 py-1.5 rounded-lg text-xs font-medium border border-surface-700 transition-all hover:text-white">
                    {{ recipe.displayName }}
                  </button>
                }
              </div>

              <!-- Selected SDK Details -->
              @if (currentSdkRecipe(); as recipe) {
                <div class="space-y-4">
                  <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 bg-surface-950 p-3.5 rounded-xl border border-surface-800">
                    <div>
                      <div class="text-xs font-semibold text-white">{{ recipe.displayName }}</div>
                      <div class="text-[11px] text-surface-400">{{ recipe.description }}</div>
                    </div>
                    <div class="flex items-center gap-2">
                      <span class="text-[10px] text-surface-400 font-mono bg-surface-900 px-2 py-1 rounded border border-surface-800">
                        {{ recipe.installationCommand }}
                      </span>
                      <button
                        (click)="copyText(recipe.installationCommand, 'Install command')"
                        class="p-1 text-surface-400 hover:text-white transition-colors"
                        title="Copy command">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 002-2M8 5a2 2 0 012-2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10m0 0l3-3m-3 3l3 3"/>
                        </svg>
                      </button>
                    </div>
                  </div>

                  <!-- Signature Verifier Code Block -->
                  <div class="space-y-2">
                    <div class="flex items-center justify-between">
                      <span class="text-xs font-semibold text-surface-300">1. Webhook Signature Verification Function</span>
                      <button
                        (click)="copyText(recipe.verificationSnippet, 'Verification snippet')"
                        class="text-[11px] text-brand-400 hover:text-brand-300 flex items-center gap-1">
                        <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 002-2M8 5a2 2 0 012-2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10m0 0l3-3m-3 3l3 3"/>
                        </svg>
                        Copy Verifier
                      </button>
                    </div>
                    <pre class="bg-surface-950 p-4 rounded-xl border border-surface-800 text-xs font-mono text-emerald-200 overflow-x-auto max-h-96"><code>{{ recipe.verificationSnippet }}</code></pre>
                  </div>

                  <!-- Event Publishing Client Code Block -->
                  <div class="space-y-2 pt-2">
                    <div class="flex items-center justify-between">
                      <span class="text-xs font-semibold text-surface-300">2. Event Publishing Integration Snippet</span>
                      <button
                        (click)="copyText(recipe.publishingSnippet, 'Publishing snippet')"
                        class="text-[11px] text-brand-400 hover:text-brand-300 flex items-center gap-1">
                        <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 002-2M8 5a2 2 0 012-2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10m0 0l3-3m-3 3l3 3"/>
                        </svg>
                        Copy Publisher
                      </button>
                    </div>
                    <pre class="bg-surface-950 p-4 rounded-xl border border-surface-800 text-xs font-mono text-brand-200 overflow-x-auto max-h-80"><code>{{ recipe.publishingSnippet }}</code></pre>
                  </div>
                </div>
              }
            </section>

            <!-- REST API Reference Section -->
            <div class="space-y-8">
              <div class="border-b border-surface-800 pb-3">
                <h2 class="text-xl font-bold text-white">REST API Endpoints</h2>
                <p class="text-xs text-surface-400">Detailed parameter definitions, request headers, sample responses, and code snippets.</p>
              </div>

              @for (ep of filteredEndpoints(); track ep.id) {
                <section [id]="ep.id" class="bg-surface-900/80 rounded-2xl border border-surface-800 p-6 shadow-xl space-y-6">
                  <!-- Endpoint Title & Method Badge -->
                  <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-surface-800 pb-4">
                    <div class="space-y-1">
                      <div class="flex items-center gap-2.5">
                        <span [ngClass]="getMethodBadgeClass(ep.method)" class="px-2.5 py-1 rounded text-xs font-bold font-mono">
                          {{ ep.method }}
                        </span>
                        <span class="font-mono text-sm font-semibold text-surface-100">{{ ep.path }}</span>
                      </div>
                      <h3 class="text-base font-bold text-white">{{ ep.summary }}</h3>
                      <p class="text-xs text-surface-400">{{ ep.description }}</p>
                    </div>

                    <!-- Action Link Shortcuts -->
                    <div class="flex items-center gap-2">
                      @if (ep.path.includes('/events/publish')) {
                        <a routerLink="/payloads" class="px-2.5 py-1 text-[11px] bg-surface-800 hover:bg-surface-700 text-brand-300 rounded border border-surface-700 transition-colors">
                          Open in Payload Inspector
                        </a>
                      }
                      @if (ep.path.includes('/traces')) {
                        <a routerLink="/traces" class="px-2.5 py-1 text-[11px] bg-surface-800 hover:bg-surface-700 text-brand-300 rounded border border-surface-700 transition-colors">
                          Open Trace Explorer
                        </a>
                      }
                      @if (ep.path.includes('/schemas')) {
                        <a routerLink="/schemas" class="px-2.5 py-1 text-[11px] bg-surface-800 hover:bg-surface-700 text-brand-300 rounded border border-surface-700 transition-colors">
                          Schema Registry
                        </a>
                      }
                      @if (ep.path.includes('/endpoints')) {
                        <a routerLink="/endpoints" class="px-2.5 py-1 text-[11px] bg-surface-800 hover:bg-surface-700 text-brand-300 rounded border border-surface-700 transition-colors">
                          Manage Endpoints
                        </a>
                      }
                    </div>
                  </div>

                  <!-- Headers Table -->
                  @if (ep.requestHeaders && ep.requestHeaders.length > 0) {
                    <div class="space-y-2">
                      <div class="text-xs font-bold text-surface-300 uppercase tracking-wider">Request Headers</div>
                      <div class="overflow-x-auto">
                        <table class="w-full text-left text-xs border border-surface-800 rounded-lg overflow-hidden">
                          <thead class="bg-surface-950 text-surface-400 text-[11px]">
                            <tr>
                              <th class="p-2.5">Header</th>
                              <th class="p-2.5">Required</th>
                              <th class="p-2.5">Description</th>
                              <th class="p-2.5">Example</th>
                            </tr>
                          </thead>
                          <tbody class="divide-y divide-surface-800/60 bg-surface-900/40 font-mono text-[11px]">
                            @for (hdr of ep.requestHeaders; track hdr.name) {
                              <tr>
                                <td class="p-2.5 text-brand-300 font-semibold">{{ hdr.name }}</td>
                                <td class="p-2.5 text-surface-400">{{ hdr.required ? 'Yes' : 'No' }}</td>
                                <td class="p-2.5 text-surface-300 font-sans">{{ hdr.description }}</td>
                                <td class="p-2.5 text-surface-400 truncate max-w-xs">{{ hdr.example }}</td>
                              </tr>
                            }
                          </tbody>
                        </table>
                      </div>
                    </div>
                  }

                  <!-- Path/Query Parameters Table -->
                  @if (ep.queryParameters && ep.queryParameters.length > 0) {
                    <div class="space-y-2">
                      <div class="text-xs font-bold text-surface-300 uppercase tracking-wider">Query Parameters</div>
                      <div class="overflow-x-auto">
                        <table class="w-full text-left text-xs border border-surface-800 rounded-lg overflow-hidden">
                          <thead class="bg-surface-950 text-surface-400 text-[11px]">
                            <tr>
                              <th class="p-2.5">Parameter</th>
                              <th class="p-2.5">Type</th>
                              <th class="p-2.5">Required</th>
                              <th class="p-2.5">Description</th>
                            </tr>
                          </thead>
                          <tbody class="divide-y divide-surface-800/60 bg-surface-900/40 font-mono text-[11px]">
                            @for (param of ep.queryParameters; track param.name) {
                              <tr>
                                <td class="p-2.5 text-brand-300 font-semibold">{{ param.name }}</td>
                                <td class="p-2.5 text-amber-300">{{ param.type }}</td>
                                <td class="p-2.5 text-surface-400">{{ param.required ? 'Yes' : 'No' }}</td>
                                <td class="p-2.5 text-surface-300 font-sans">{{ param.description }}</td>
                              </tr>
                            }
                          </tbody>
                        </table>
                      </div>
                    </div>
                  }

                  <!-- Code Snippet Box -->
                  <div class="space-y-2">
                    <div class="flex items-center justify-between">
                      <span class="text-xs font-bold text-surface-300 uppercase tracking-wider">
                        Call in {{ selectedLanguage().toUpperCase() }}
                      </span>
                      <button
                        (click)="copyText(getActiveSnippet(ep.codeSnippets), 'API Request snippet')"
                        class="text-[11px] text-brand-400 hover:text-brand-300 flex items-center gap-1">
                        <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 002-2M8 5a2 2 0 012-2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10m0 0l3-3m-3 3l3 3"/>
                        </svg>
                        Copy Request
                      </button>
                    </div>
                    <pre class="bg-surface-950 p-4 rounded-xl border border-surface-800 text-xs font-mono text-cyan-200 overflow-x-auto max-h-72"><code>{{ getActiveSnippet(ep.codeSnippets) }}</code></pre>
                  </div>

                  <!-- Sample Request / Response Flex -->
                  <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                    @if (ep.sampleRequestBody) {
                      <div class="space-y-1.5">
                        <div class="flex items-center justify-between">
                          <span class="text-[11px] font-semibold text-surface-400">Request Body (JSON)</span>
                          <button (click)="copyText(ep.sampleRequestBody, 'Request Body')" class="text-[10px] text-surface-400 hover:text-white">Copy</button>
                        </div>
                        <pre class="bg-surface-950 p-3 rounded-lg border border-surface-800 text-[11px] font-mono text-surface-300 overflow-x-auto max-h-56"><code>{{ ep.sampleRequestBody }}</code></pre>
                      </div>
                    }

                    @if (ep.sampleResponseBody) {
                      <div class="space-y-1.5" [class.col-span-2]="!ep.sampleRequestBody">
                        <div class="flex items-center justify-between">
                          <span class="text-[11px] font-semibold text-surface-400">Response {{ ep.expectedStatusCode }} (JSON)</span>
                          <button (click)="copyText(ep.sampleResponseBody, 'Response Body')" class="text-[10px] text-surface-400 hover:text-white">Copy</button>
                        </div>
                        <pre class="bg-surface-950 p-3 rounded-lg border border-surface-800 text-[11px] font-mono text-emerald-300 overflow-x-auto max-h-56"><code>{{ ep.sampleResponseBody }}</code></pre>
                      </div>
                    }
                  </div>
                </section>
              }
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class DocsComponent implements OnInit {
  private readonly docService = inject(DocService);
  readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);

  readonly supportedLanguages = [
    { id: 'curl', label: 'cURL' },
    { id: 'typescript', label: 'TypeScript / Node' },
    { id: 'csharp', label: 'C# / .NET 10' },
    { id: 'python', label: 'Python' },
    { id: 'go', label: 'Go' },
    { id: 'php', label: 'PHP' }
  ];

  readonly selectedLanguage = signal<string>('curl');
  readonly selectedSdkTab = signal<string>('typescript');
  readonly searchQuery = signal<string>('');
  readonly apiReference = signal<ApiReferenceResponse | null>(null);
  readonly loading = signal<boolean>(true);

  // Sandbox Signals
  readonly sandboxSecret = signal<string>('whsec_live_example_secret_key_8f9e');
  readonly sandboxTimestamp = signal<number>(Math.floor(Date.now() / 1000));
  readonly sandboxPayload = signal<string>('{"orderId": "ord_9981", "total": 129.50, "currency": "USD"}');
  readonly sandboxHeader = signal<string>('');
  readonly sandboxVerifying = signal<boolean>(false);
  readonly sandboxResult = signal<{ isValid: boolean; clockSkewSeconds?: number } | null>(null);

  readonly filteredGuides = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    const guides = this.apiReference()?.guides || [];
    if (!q) return guides;
    return guides.filter(g =>
      g.title.toLowerCase().includes(q) ||
      g.summary.toLowerCase().includes(q) ||
      g.category.toLowerCase().includes(q)
    );
  });

  readonly filteredEndpoints = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    const eps = this.apiReference()?.endpoints || [];
    if (!q) return eps;
    return eps.filter(e =>
      e.path.toLowerCase().includes(q) ||
      e.summary.toLowerCase().includes(q) ||
      e.description.toLowerCase().includes(q) ||
      e.method.toLowerCase().includes(q) ||
      e.category.toLowerCase().includes(q)
    );
  });

  readonly groupedEndpoints = computed(() => {
    const eps = this.filteredEndpoints();
    const groups: { category: string; endpoints: DocEndpointDto[] }[] = [];
    const map = new Map<string, DocEndpointDto[]>();

    for (const ep of eps) {
      if (!map.has(ep.category)) {
        map.set(ep.category, []);
      }
      map.get(ep.category)!.push(ep);
    }

    for (const [category, items] of map.entries()) {
      groups.push({ category, endpoints: items });
    }

    return groups;
  });

  readonly currentSdkRecipe = computed<SdkRecipeDto | null>(() => {
    const recipes = this.apiReference()?.sdkRecipes || [];
    return recipes.find(r => r.language === this.selectedSdkTab()) || recipes[0] || null;
  });

  ngOnInit(): void {
    this.loadApiReference();
    this.generateSandboxSignature();
  }

  loadApiReference(): void {
    this.loading.set(true);
    this.docService.getApiReference().subscribe({
      next: (ref) => {
        this.apiReference.set(ref);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Failed to load documentation reference.');
      }
    });
  }

  setLanguage(langId: string): void {
    this.selectedLanguage.set(langId);
    if (['typescript', 'csharp', 'python', 'go', 'php'].includes(langId)) {
      this.selectedSdkTab.set(langId);
    }
  }

  getActiveSnippet(snippets: Record<string, string> | undefined): string {
    if (!snippets) return '';
    const lang = this.selectedLanguage();
    return snippets[lang] || snippets['curl'] || snippets['typescript'] || Object.values(snippets)[0] || '';
  }

  getMethodBadgeClass(method: string): string {
    switch (method.toUpperCase()) {
      case 'GET': return 'bg-sky-500/20 text-sky-300 border border-sky-500/30';
      case 'POST': return 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30';
      case 'PUT': return 'bg-amber-500/20 text-amber-300 border border-amber-500/30';
      case 'DELETE': return 'bg-rose-500/20 text-rose-300 border border-rose-500/30';
      default: return 'bg-surface-700 text-surface-300';
    }
  }

  scrollToSection(id: string): void {
    const element = document.getElementById(id);
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  copyText(text: string, label: string): void {
    navigator.clipboard.writeText(text).then(() => {
      this.toast.success(`${label} copied to clipboard.`);
    });
  }

  copyBaseUrl(): void {
    const url = this.apiReference()?.baseUrl || window.location.origin;
    this.copyText(url, 'Base URL');
  }

  setSandboxTimestampToNow(): void {
    this.sandboxTimestamp.set(Math.floor(Date.now() / 1000));
    this.generateSandboxSignature();
  }

  loadSandboxPreset(preset: 'order' | 'invoice'): void {
    if (preset === 'order') {
      this.sandboxPayload.set(JSON.stringify({
        id: 'ord_9981',
        customer: { email: 'alex@example.com' },
        amount: 129.50,
        currency: 'USD'
      }, null, 2));
    } else {
      this.sandboxPayload.set(JSON.stringify({
        invoiceId: 'inv_4401',
        status: 'PAID',
        paidAt: new Date().toISOString(),
        total: 450.00
      }, null, 2));
    }
    this.generateSandboxSignature();
  }

  generateSandboxSignature(): void {
    const timestamp = this.sandboxTimestamp();
    const raw = this.sandboxPayload();
    const secret = this.sandboxSecret();

    const fakeSig = this.simpleHexHmac(secret, `${timestamp}.${raw}`);
    const header = `t=${timestamp},v1=${fakeSig}`;
    this.sandboxHeader.set(header);
    this.sandboxResult.set({ isValid: true, clockSkewSeconds: Math.abs(Math.floor(Date.now() / 1000) - timestamp) });
  }

  verifySandboxSignature(): void {
    this.sandboxVerifying.set(true);
    const secret = this.sandboxSecret();
    const header = this.sandboxHeader();
    const payload = this.sandboxPayload();

    this.docService.verifySignatureInteractive(secret, header, payload).subscribe({
      next: (res) => {
        this.sandboxVerifying.set(false);
        this.sandboxResult.set({
          isValid: res.isValid,
          clockSkewSeconds: res.clockSkewSeconds
        });
        if (res.isValid) {
          this.toast.success('Signature verified successfully!');
        } else {
          this.toast.error('Signature verification failed.');
        }
      },
      error: () => {
        this.sandboxVerifying.set(false);
        const timestampMatch = header.match(/t=(\d+)/);
        const timestamp = timestampMatch ? parseInt(timestampMatch[1], 10) : 0;
        const skew = Math.abs(Math.floor(Date.now() / 1000) - timestamp);
        const isValid = header.includes('v1=') && skew <= 300;
        this.sandboxResult.set({ isValid, clockSkewSeconds: skew });
      }
    });
  }

  renderMarkdown(markdown: string): string {
    if (!markdown) return '';
    return markdown
      .replace(/### (.*?)\n/g, '<h4 class="text-sm font-bold text-surface-100 mt-2 mb-1">$1</h4>')
      .replace(/`([^`]+)`/g, '<code class="bg-surface-900 text-brand-300 px-1 py-0.5 rounded font-mono text-[11px]">$1</code>')
      .replace(/\*\*([^*]+)\*\*/g, '<strong class="text-surface-100 font-semibold">$1</strong>')
      .replace(/\n\n/g, '<br/>');
  }

  private simpleHexHmac(key: string, message: string): string {
    let hash = 0;
    const str = key + message;
    for (let i = 0; i < str.length; i++) {
      const char = str.charCodeAt(i);
      hash = ((hash << 5) - hash) + char;
      hash = hash & hash;
    }
    const hex = Math.abs(hash).toString(16).padStart(8, '0');
    return (hex + hex + hex + hex + hex + hex + hex + hex).slice(0, 64);
  }
}
