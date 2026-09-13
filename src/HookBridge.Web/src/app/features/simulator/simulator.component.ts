import { Component, OnInit, OnDestroy, computed, inject, signal, effect, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SimulatorService } from '../../core/services/simulator.service';
import { SignalRService } from '../../core/signalr/services/signalr.service';
import { ToastService } from '../../shared/components/ui/toast/toast.service';
import {
  SimulatorRule,
  SimulatorExecution,
  SimulatorStats,
  SimulatorStrategy,
  CreateSimulatorRuleRequest,
  UpdateSimulatorRuleRequest,
  TestDispatchRequest,
  SimulatedExecutionResult
} from '../../core/models/simulator.models';
import { CodeViewerComponent } from '../../shared/components/ui/code-viewer.component';
import { SkeletonLoaderComponent } from '../../shared/components/ui/skeleton-loader.component';

interface QuickPreset {
  name: string;
  badge: string;
  color: string;
  description: string;
  urlPath: string;
  statusCode: number;
  delayMs?: number;
  retryAfter?: number;
}

@Component({
  selector: 'app-simulator',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    CodeViewerComponent,
    SkeletonLoaderComponent
  ],
  template: `
    <div class="space-y-6 max-w-7xl mx-auto pb-16">
      <!-- Header Banner -->
      <div class="bg-gradient-to-r from-surface-900 via-surface-900/95 to-amber-950/40 p-6 rounded-2xl border border-surface-800 shadow-xl flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div class="flex items-center gap-2">
            <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-500/20 text-amber-300 border border-amber-500/30">
              Chaos & Failure Simulator
            </span>
            <span class="flex items-center gap-1.5 text-xs text-surface-400 font-mono">
              <span class="w-2 h-2 rounded-full" [ngClass]="signalR.status() === 'connected' ? 'bg-emerald-400 animate-pulse' : 'bg-amber-400'"></span>
              {{ signalR.status() === 'connected' ? 'Live Stream Active' : 'Connecting...' }}
            </span>
          </div>
          <h1 class="text-2xl font-bold text-white mt-1">Delivery Failure Simulator & Chaos Lab</h1>
          <p class="text-xs text-surface-400 mt-0.5">
            Test and verify webhook retry policies, backoff handling, and dead-letter routing by injecting HTTP 429, 500, timeouts, flakiness, and custom fault scenarios.
          </p>
        </div>

        <!-- Action Buttons -->
        <div class="flex items-center gap-3">
          <button
            (click)="openCreateModal()"
            class="px-4 py-2 rounded-xl bg-amber-600 hover:bg-amber-500 text-white text-xs font-semibold shadow-lg shadow-amber-600/30 transition-all flex items-center gap-2">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
            </svg>
            <span>New Chaos Rule</span>
          </button>
          <button
            (click)="refreshAll()"
            [disabled]="loadingRules()"
            class="p-2 rounded-xl bg-surface-800 hover:bg-surface-700 text-surface-300 hover:text-white border border-surface-700 transition-colors"
            title="Refresh">
            <svg class="w-4 h-4" [class.animate-spin]="loadingRules()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
            </svg>
          </button>
        </div>
      </div>

      <!-- Quick Presets Toolbar -->
      <div class="bg-surface-900/90 p-5 rounded-2xl border border-surface-800 shadow-md space-y-3">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <svg class="w-4 h-4 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/>
            </svg>
            <span class="text-xs font-bold uppercase tracking-wider text-surface-300">Instant Chaos Endpoints (No Config Required)</span>
          </div>
          <span class="text-[11px] text-surface-500">Click any preset to copy its direct dispatch URL</span>
        </div>

        <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-2.5">
          @for (preset of quickPresets; track preset.name) {
            <button
              (click)="copyPresetUrl(preset)"
              class="p-3 rounded-xl bg-surface-800/60 hover:bg-surface-800 border border-surface-700/60 hover:border-amber-500/40 text-left transition-all group flex flex-col justify-between">
              <div>
                <div class="flex items-center justify-between gap-1 mb-1">
                  <span class="text-xs font-semibold text-white group-hover:text-amber-300 transition-colors">{{ preset.name }}</span>
                  <span class="px-1.5 py-0.5 rounded text-[10px] font-mono font-bold" [ngClass]="preset.color">
                    {{ preset.badge }}
                  </span>
                </div>
                <div class="text-[11px] text-surface-400 line-clamp-1">{{ preset.description }}</div>
              </div>
              <div class="mt-2 text-[10px] text-surface-500 font-mono truncate flex items-center gap-1 group-hover:text-amber-400">
                <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"/>
                </svg>
                <span>Copy URL</span>
              </div>
            </button>
          }
        </div>
      </div>

      <!-- KPI Summary Cards -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <!-- Total Invocations -->
        <div class="bg-surface-900/90 p-5 rounded-2xl border border-surface-800 shadow-md flex items-center justify-between">
          <div>
            <div class="text-xs font-semibold uppercase tracking-wider text-surface-400">Total Invocations</div>
            <div class="text-2xl font-extrabold text-white mt-1">{{ stats()?.totalExecutions ?? 0 | number }}</div>
            <div class="text-[11px] text-surface-500 mt-0.5">Recorded simulator hits</div>
          </div>
          <div class="w-11 h-11 rounded-xl bg-surface-800 border border-surface-700 flex items-center justify-center text-surface-300">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/>
            </svg>
          </div>
        </div>

        <!-- Failure Injection Rate -->
        <div class="bg-surface-900/90 p-5 rounded-2xl border border-surface-800 shadow-md flex items-center justify-between">
          <div>
            <div class="text-xs font-semibold uppercase tracking-wider text-surface-400">Failure Injection Rate</div>
            <div class="text-2xl font-extrabold text-rose-400 mt-1">
              {{ stats()?.overallFailureRatePercent ?? 0 | number:'1.1-1' }}%
            </div>
            <div class="text-[11px] text-surface-500 mt-0.5">
              {{ stats()?.totalFailures ?? 0 }} simulated failures
            </div>
          </div>
          <div class="w-11 h-11 rounded-xl bg-rose-500/10 border border-rose-500/20 flex items-center justify-center text-rose-400">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>
            </svg>
          </div>
        </div>

        <!-- Average Latency -->
        <div class="bg-surface-900/90 p-5 rounded-2xl border border-surface-800 shadow-md flex items-center justify-between">
          <div>
            <div class="text-xs font-semibold uppercase tracking-wider text-surface-400">Avg Simulated Delay</div>
            <div class="text-2xl font-extrabold text-amber-400 mt-1">
              {{ stats()?.averageLatencyMs ?? 0 | number:'1.0-0' }} ms
            </div>
            <div class="text-[11px] text-surface-500 mt-0.5">Response latency added</div>
          </div>
          <div class="w-11 h-11 rounded-xl bg-amber-500/10 border border-amber-500/20 flex items-center justify-center text-amber-400">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/>
            </svg>
          </div>
        </div>

        <!-- Active Chaos Rules -->
        <div class="bg-surface-900/90 p-5 rounded-2xl border border-surface-800 shadow-md flex items-center justify-between">
          <div>
            <div class="text-xs font-semibold uppercase tracking-wider text-surface-400">Active Chaos Rules</div>
            <div class="text-2xl font-extrabold text-emerald-400 mt-1">
              {{ stats()?.activeRulesCount ?? 0 }}
            </div>
            <div class="text-[11px] text-surface-500 mt-0.5">{{ rules().length }} configured profiles</div>
          </div>
          <div class="w-11 h-11 rounded-xl bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center text-emerald-400">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/>
            </svg>
          </div>
        </div>
      </div>

      <!-- Navigation Tabs -->
      <div class="flex items-center gap-2 border-b border-surface-800 pb-2">
        <button
          (click)="activeTab.set('rules')"
          [class.bg-amber-600]="activeTab() === 'rules'"
          [class.text-white]="activeTab() === 'rules'"
          [class.bg-surface-800]="activeTab() !== 'rules'"
          [class.text-surface-400]="activeTab() !== 'rules'"
          class="px-4 py-2 rounded-xl text-xs font-semibold transition-all flex items-center gap-2">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z"/>
          </svg>
          <span>Chaos Profiles & Rules ({{ rules().length }})</span>
        </button>

        <button
          (click)="activeTab.set('executions')"
          [class.bg-amber-600]="activeTab() === 'executions'"
          [class.text-white]="activeTab() === 'executions'"
          [class.bg-surface-800]="activeTab() !== 'executions'"
          [class.text-surface-400]="activeTab() !== 'executions'"
          class="px-4 py-2 rounded-xl text-xs font-semibold transition-all flex items-center gap-2">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/>
          </svg>
          <span>Live Executions & History</span>
          @if (executions().length > 0) {
            <span class="px-1.5 py-0.2 rounded-full text-[10px] bg-surface-900 text-amber-300 font-mono">{{ totalExecutionsCount() }}</span>
          }
        </button>

        <button
          (click)="activeTab.set('workbench')"
          [class.bg-amber-600]="activeTab() === 'workbench'"
          [class.text-white]="activeTab() === 'workbench'"
          [class.bg-surface-800]="activeTab() !== 'workbench'"
          [class.text-surface-400]="activeTab() !== 'workbench'"
          class="px-4 py-2 rounded-xl text-xs font-semibold transition-all flex items-center gap-2">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/>
          </svg>
          <span>Test Dispatch Workbench</span>
        </button>
      </div>

      <!-- TAB 1: RULES & CHAOS PROFILES -->
      @if (activeTab() === 'rules') {
        @if (loadingRules()) {
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <app-skeleton-loader [customClass]="'h-48 w-full rounded-2xl'"></app-skeleton-loader>
            <app-skeleton-loader [customClass]="'h-48 w-full rounded-2xl'"></app-skeleton-loader>
          </div>
        } @else if (rules().length === 0) {
          <div class="bg-surface-900/80 p-12 rounded-2xl border border-surface-800 text-center space-y-4">
            <div class="w-16 h-16 rounded-2xl bg-amber-500/10 text-amber-400 flex items-center justify-center mx-auto">
              <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/>
              </svg>
            </div>
            <div>
              <h3 class="text-base font-bold text-white">No Chaos Rules Configured Yet</h3>
              <p class="text-xs text-surface-400 max-w-md mx-auto mt-1">
                Create a custom rule to simulate specific HTTP failures, transient sequential retries, high latency timeouts, or random flakiness.
              </p>
            </div>
            <button
              (click)="openCreateModal()"
              class="px-5 py-2.5 rounded-xl bg-amber-600 hover:bg-amber-500 text-white text-xs font-bold shadow-lg shadow-amber-600/30 transition-all">
              Create Your First Rule
            </button>
          </div>
        } @else {
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
            @for (rule of rules(); track rule.id) {
              <div class="bg-surface-900/90 rounded-2xl border border-surface-800 p-5 shadow-lg space-y-4 hover:border-surface-700 transition-all flex flex-col justify-between">
                <div>
                  <!-- Header -->
                  <div class="flex items-start justify-between gap-3">
                    <div class="space-y-1">
                      <div class="flex items-center gap-2">
                        <h3 class="text-sm font-bold text-white">{{ rule.name }}</h3>
                        <span class="px-2 py-0.5 rounded text-[10px] font-semibold border font-mono"
                          [ngClass]="getStrategyBadgeClass(rule.strategy)">
                          {{ rule.strategyName }}
                        </span>
                        @if (!rule.isActive) {
                          <span class="px-2 py-0.5 rounded text-[10px] bg-surface-800 text-surface-400 border border-surface-700">Paused</span>
                        }
                      </div>
                      @if (rule.description) {
                        <p class="text-xs text-surface-400">{{ rule.description }}</p>
                      }
                    </div>

                    <!-- Target Status Code Badge -->
                    <span class="px-2.5 py-1 rounded-lg text-xs font-mono font-bold border"
                      [ngClass]="getStatusCodeBadgeClass(rule.targetStatusCode)">
                      HTTP {{ rule.targetStatusCode }}
                    </span>
                  </div>

                  <!-- Receiver URL Bar -->
                  <div class="mt-3 p-2.5 rounded-xl bg-surface-950/80 border border-surface-800/80 flex items-center justify-between gap-2">
                    <span class="text-[11px] text-surface-400 font-mono truncate select-all">{{ rule.receiverUrl }}</span>
                    <button
                      (click)="copyToClipboard(rule.receiverUrl, 'Receiver URL copied')"
                      class="px-2.5 py-1 rounded-lg bg-surface-800 hover:bg-surface-700 text-surface-300 hover:text-white text-[11px] font-semibold border border-surface-700 transition-colors shrink-0 flex items-center gap-1.5">
                      <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"/>
                      </svg>
                      <span>Copy</span>
                    </button>
                  </div>

                  <!-- Parameters Grid -->
                  <div class="grid grid-cols-3 gap-2 mt-3 pt-3 border-t border-surface-800/60 text-[11px]">
                    <div>
                      <span class="text-surface-500 block text-[10px] uppercase tracking-wider">Delay / Latency</span>
                      <span class="text-surface-200 font-mono font-medium">{{ rule.delayMs }} ms</span>
                    </div>

                    @if (rule.strategy === 'FailureRate' || rule.strategy === 1) {
                      <div>
                        <span class="text-surface-500 block text-[10px] uppercase tracking-wider">Failure Rate</span>
                        <span class="text-rose-400 font-mono font-bold">{{ rule.failureRatePercent }}%</span>
                      </div>
                    } @else if (rule.strategy === 'SequentialRetryPattern' || rule.strategy === 2) {
                      <div>
                        <span class="text-surface-500 block text-[10px] uppercase tracking-wider">Step Counter</span>
                        <div class="flex items-center gap-1.5">
                          <span class="text-amber-400 font-mono font-bold">{{ rule.currentStepCount }}/{{ rule.failureStepCount }}</span>
                          <button
                            (click)="resetRuleSteps(rule.id)"
                            class="text-[10px] text-surface-400 hover:text-white underline"
                            title="Reset attempt counter back to 0">
                            Reset
                          </button>
                        </div>
                      </div>
                    } @else {
                      <div>
                        <span class="text-surface-500 block text-[10px] uppercase tracking-wider">Success Code</span>
                        <span class="text-emerald-400 font-mono font-medium">{{ rule.successStatusCode }}</span>
                      </div>
                    }

                    <div>
                      <span class="text-surface-500 block text-[10px] uppercase tracking-wider">Observed Hits</span>
                      <span class="text-surface-200 font-mono font-bold">{{ rule.totalExecutions }} hits ({{ rule.failureRateObservedPercent }}% fail)</span>
                    </div>
                  </div>
                </div>

                <!-- Footer Actions -->
                <div class="flex items-center justify-between gap-2 pt-3 border-t border-surface-800">
                  <button
                    (click)="triggerWorkbenchTest(rule)"
                    class="px-3 py-1.5 rounded-lg bg-surface-800 hover:bg-surface-700 text-amber-300 hover:text-white text-xs font-semibold border border-surface-700 transition-colors flex items-center gap-1.5">
                    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z"/>
                    </svg>
                    <span>Test Dispatch</span>
                  </button>

                  <div class="flex items-center gap-2">
                    <button
                      (click)="openEditModal(rule)"
                      class="px-3 py-1.5 rounded-lg bg-surface-800 hover:bg-surface-700 text-surface-300 hover:text-white text-xs font-medium border border-surface-700 transition-colors">
                      Edit
                    </button>
                    <button
                      (click)="deleteRule(rule.id)"
                      class="p-1.5 rounded-lg bg-surface-800 hover:bg-rose-900/40 text-surface-400 hover:text-rose-300 border border-surface-700 transition-colors"
                      title="Delete Rule">
                      <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/>
                      </svg>
                    </button>
                  </div>
                </div>
              </div>
            }
          </div>
        }
      }

      <!-- TAB 2: LIVE EXECUTIONS & HISTORY -->
      @if (activeTab() === 'executions') {
        <div class="bg-surface-900/90 rounded-2xl border border-surface-800 shadow-xl overflow-hidden space-y-4 p-5">
          <!-- Filters Bar -->
          <div class="flex flex-col md:flex-row items-center justify-between gap-3">
            <div class="flex items-center gap-2 w-full md:w-auto">
              <input
                type="text"
                [(ngModel)]="executionSearchTerm"
                (ngModelChange)="applyExecutionFilters()"
                placeholder="Search path, fault, payload..."
                class="px-3 py-2 rounded-xl bg-surface-950 border border-surface-800 text-xs text-white placeholder-surface-500 focus:outline-none focus:border-amber-500 w-full md:w-64"
              />
              <select
                [(ngModel)]="executionStatusFilter"
                (ngModelChange)="applyExecutionFilters()"
                class="px-3 py-2 rounded-xl bg-surface-950 border border-surface-800 text-xs text-white focus:outline-none focus:border-amber-500">
                <option [ngValue]="null">All Statuses</option>
                <option [ngValue]="200">200 OK</option>
                <option [ngValue]="429">429 Rate Limit</option>
                <option [ngValue]="500">500 Server Error</option>
                <option [ngValue]="502">502 Bad Gateway</option>
                <option [ngValue]="503">503 Service Unavailable</option>
                <option [ngValue]="504">504 Gateway Timeout</option>
              </select>
            </div>

            <div class="flex items-center gap-2">
              <button
                (click)="toggleStreamPause()"
                class="px-3 py-1.5 rounded-xl text-xs font-semibold border transition-colors flex items-center gap-1.5"
                [ngClass]="isStreamPaused() ? 'bg-amber-500/20 text-amber-300 border-amber-500/40' : 'bg-surface-800 text-surface-300 border-surface-700 hover:text-white'">
                <span>{{ isStreamPaused() ? 'Stream Paused' : 'Live Stream' }}</span>
              </button>
              <button
                (click)="clearExecutions()"
                class="px-3 py-1.5 rounded-xl bg-surface-800 hover:bg-rose-900/40 text-surface-400 hover:text-rose-300 text-xs font-medium border border-surface-700 transition-colors">
                Clear Logs
              </button>
            </div>
          </div>

          <!-- Executions Table -->
          @if (loadingExecutions()) {
            <app-skeleton-loader [customClass]="'h-40 w-full rounded-xl'"></app-skeleton-loader>
          } @else if (executions().length === 0) {
            <div class="text-center py-12 space-y-2">
              <div class="text-surface-500 text-sm">No simulator executions recorded yet.</div>
              <div class="text-xs text-surface-600">Send a webhook to any simulator URL or use the workbench to test.</div>
            </div>
          } @else {
            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs text-surface-300">
                <thead class="bg-surface-950/80 text-surface-400 font-semibold uppercase tracking-wider text-[10px] border-b border-surface-800">
                  <tr>
                    <th class="p-3">Status</th>
                    <th class="p-3">Method & Path</th>
                    <th class="p-3">Injected Fault</th>
                    <th class="p-3">Duration</th>
                    <th class="p-3">Rule / Preset</th>
                    <th class="p-3">Time</th>
                    <th class="p-3 text-right">Inspect</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-surface-800/60 font-mono">
                  @for (exec of executions(); track exec.id) {
                    <tr class="hover:bg-surface-800/40 transition-colors group cursor-pointer" (click)="selectExecution(exec)">
                      <td class="p-3">
                        <span class="px-2 py-0.5 rounded text-[10px] font-bold border"
                          [ngClass]="getStatusCodeBadgeClass(exec.simulatedStatusCode)">
                          {{ exec.simulatedStatusCode }}
                        </span>
                      </td>
                      <td class="p-3">
                        <div class="flex items-center gap-2">
                          <span class="px-1.5 py-0.2 rounded text-[9px] font-bold bg-surface-800 text-surface-300">{{ exec.httpMethod }}</span>
                          <span class="truncate max-w-[200px] text-white font-normal">{{ exec.path }}</span>
                        </div>
                      </td>
                      <td class="p-3">
                        <span class="text-amber-400 font-sans font-medium text-[11px]">{{ exec.injectedFault }}</span>
                      </td>
                      <td class="p-3 font-normal text-surface-400">
                        {{ exec.executionDurationMs | number:'1.0-0' }} ms
                      </td>
                      <td class="p-3 font-sans text-surface-400 text-[11px]">
                        {{ exec.ruleName || 'Ad-Hoc' }}
                      </td>
                      <td class="p-3 text-surface-500 text-[10px] whitespace-nowrap font-sans">
                        {{ exec.executedAt | date:'HH:mm:ss' }}
                      </td>
                      <td class="p-3 text-right font-sans">
                        <button
                          (click)="selectExecution(exec); $event.stopPropagation()"
                          class="px-2.5 py-1 rounded-lg bg-surface-800 hover:bg-surface-700 text-surface-300 hover:text-white text-[11px] font-medium border border-surface-700 transition-colors">
                          Inspect
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      }

      <!-- TAB 3: TEST DISPATCH WORKBENCH -->
      @if (activeTab() === 'workbench') {
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <!-- Dispatch Form -->
          <div class="bg-surface-900/90 rounded-2xl border border-surface-800 p-6 shadow-xl space-y-4">
            <div class="flex items-center justify-between">
              <div>
                <h3 class="text-sm font-bold text-white">Webhook Dispatch Workbench</h3>
                <p class="text-xs text-surface-400 mt-0.5">Send immediate test webhook requests directly to your simulator rules.</p>
              </div>
              <span class="px-2 py-0.5 rounded text-[10px] bg-amber-500/10 text-amber-300 border border-amber-500/20 font-mono">
                Interactive Test Tool
              </span>
            </div>

            <!-- Target Selection -->
            <div class="space-y-1.5">
              <label class="text-xs font-semibold text-surface-300">Target Rule / Endpoint</label>
              <select
                [(ngModel)]="workbenchTarget"
                class="w-full px-3.5 py-2.5 rounded-xl bg-surface-950 border border-surface-800 text-xs text-white focus:outline-none focus:border-amber-500">
                <option value="ad-hoc-429">Ad-Hoc: 429 Too Many Requests (Retry-After: 30s)</option>
                <option value="ad-hoc-500">Ad-Hoc: 500 Internal Server Error</option>
                <option value="ad-hoc-503">Ad-Hoc: 503 Service Unavailable</option>
                <option value="ad-hoc-timeout">Ad-Hoc: Timeout (5000ms delay -> 504)</option>
                <option value="ad-hoc-chaos">Ad-Hoc: 50% Chaos Flakiness</option>
                @for (rule of rules(); track rule.id) {
                  <option [value]="rule.id">Rule: {{ rule.name }} (HTTP {{ rule.targetStatusCode }})</option>
                }
              </select>
            </div>

            <!-- HTTP Method & Headers -->
            <div class="grid grid-cols-3 gap-3">
              <div class="space-y-1.5">
                <label class="text-xs font-semibold text-surface-300">Method</label>
                <select
                  [(ngModel)]="workbenchMethod"
                  class="w-full px-3 py-2.5 rounded-xl bg-surface-950 border border-surface-800 text-xs text-white focus:outline-none focus:border-amber-500 font-mono">
                  <option value="POST">POST</option>
                  <option value="PUT">PUT</option>
                  <option value="GET">GET</option>
                  <option value="DELETE">DELETE</option>
                  <option value="PATCH">PATCH</option>
                </select>
              </div>

              <div class="col-span-2 space-y-1.5">
                <label class="text-xs font-semibold text-surface-300">Event Header (X-Webhook-Event)</label>
                <input
                  type="text"
                  [(ngModel)]="workbenchEventHeader"
                  placeholder="invoice.payment_succeeded"
                  class="w-full px-3.5 py-2.5 rounded-xl bg-surface-950 border border-surface-800 text-xs text-white placeholder-surface-600 focus:outline-none focus:border-amber-500 font-mono"
                />
              </div>
            </div>

            <!-- Request Body Editor -->
            <div class="space-y-1.5">
              <div class="flex items-center justify-between">
                <label class="text-xs font-semibold text-surface-300">Payload Body (JSON)</label>
                <button
                  (click)="loadSamplePayload()"
                  class="text-[11px] text-amber-400 hover:text-amber-300 underline">
                  Load Sample
                </button>
              </div>
              <textarea
                [(ngModel)]="workbenchBody"
                rows="7"
                class="w-full p-3 rounded-xl bg-surface-950 border border-surface-800 text-xs text-amber-200 font-mono focus:outline-none focus:border-amber-500 resize-none">
              </textarea>
            </div>

            <!-- Submit Button -->
            <button
              (click)="executeWorkbenchDispatch()"
              [disabled]="dispatchingTest()"
              class="w-full py-3 rounded-xl bg-gradient-to-r from-amber-600 to-amber-500 hover:from-amber-500 hover:to-amber-400 text-white text-xs font-bold shadow-lg shadow-amber-600/30 transition-all flex items-center justify-center gap-2 disabled:opacity-50">
              @if (dispatchingTest()) {
                <svg class="w-4 h-4 animate-spin" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
                </svg>
                <span>Simulating Dispatch...</span>
              } @else {
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z"/>
                </svg>
                <span>Dispatch Test Webhook</span>
              }
            </button>
          </div>

          <!-- Dispatch Result Box -->
          <div class="bg-surface-900/90 rounded-2xl border border-surface-800 p-6 shadow-xl flex flex-col justify-between space-y-4">
            <div>
              <div class="flex items-center justify-between pb-3 border-b border-surface-800">
                <h3 class="text-sm font-bold text-white">Simulation Execution Result</h3>
                @if (workbenchResult()) {
                  <span class="px-2.5 py-1 rounded-lg text-xs font-mono font-bold border"
                    [ngClass]="getStatusCodeBadgeClass(workbenchResult()!.statusCode)">
                    HTTP {{ workbenchResult()!.statusCode }}
                  </span>
                } @else {
                  <span class="text-xs text-surface-500">Awaiting dispatch</span>
                }
              </div>

              @if (workbenchResult()) {
                <div class="space-y-4 mt-4">
                  <!-- Timing & Fault Grid -->
                  <div class="grid grid-cols-2 gap-3 p-3 rounded-xl bg-surface-950 border border-surface-800 text-xs">
                    <div>
                      <span class="text-surface-500 block text-[10px] uppercase tracking-wider">Injected Fault</span>
                      <span class="text-amber-300 font-medium">{{ workbenchResult()!.injectedFault }}</span>
                    </div>
                    <div>
                      <span class="text-surface-500 block text-[10px] uppercase tracking-wider">Execution Duration</span>
                      <span class="text-surface-200 font-mono font-bold">{{ workbenchResult()!.durationMs | number:'1.1-1' }} ms</span>
                    </div>
                  </div>

                  <!-- Response Headers -->
                  <div class="space-y-1">
                    <span class="text-xs font-semibold text-surface-300">Simulated Response Headers</span>
                    <app-code-viewer [code]="formatJson(workbenchResult()!.headers)" language="json"></app-code-viewer>
                  </div>

                  <!-- Response Body -->
                  <div class="space-y-1">
                    <span class="text-xs font-semibold text-surface-300">Simulated Response Body</span>
                    <app-code-viewer [code]="workbenchResult()!.body || ''" language="json"></app-code-viewer>
                  </div>
                </div>
              } @else {
                <div class="text-center py-20 space-y-3">
                  <div class="w-12 h-12 rounded-xl bg-surface-800/80 text-surface-500 flex items-center justify-center mx-auto">
                    <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/>
                    </svg>
                  </div>
                  <div class="text-xs text-surface-400">Click "Dispatch Test Webhook" to execute simulation and view live telemetry.</div>
                </div>
              }
            </div>
          </div>
        </div>
      }

      <!-- INSPECT EXECUTION SLIDEOVER DRAWER -->
      @if (selectedExecution()) {
        <div class="fixed inset-0 z-50 flex justify-end">
          <div class="fixed inset-0 bg-black/60 backdrop-blur-xs" (click)="selectedExecution.set(null)"></div>
          <div class="relative w-full max-w-xl bg-surface-900 border-l border-surface-800 shadow-2xl p-6 overflow-y-auto z-10 space-y-5">
            <!-- Header -->
            <div class="flex items-center justify-between pb-4 border-b border-surface-800">
              <div>
                <div class="flex items-center gap-2">
                  <span class="px-2 py-0.5 rounded text-xs font-mono font-bold border"
                    [ngClass]="getStatusCodeBadgeClass(selectedExecution()!.simulatedStatusCode)">
                    HTTP {{ selectedExecution()!.simulatedStatusCode }}
                  </span>
                  <span class="text-xs font-semibold text-amber-300">{{ selectedExecution()!.injectedFault }}</span>
                </div>
                <div class="text-[11px] text-surface-500 font-mono mt-1">{{ selectedExecution()!.executedAt | date:'medium' }}</div>
              </div>

              <button
                (click)="selectedExecution.set(null)"
                class="p-2 rounded-xl bg-surface-800 hover:bg-surface-700 text-surface-400 hover:text-white transition-colors">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
                </svg>
              </button>
            </div>

            <!-- Inbound Request Details -->
            <div class="space-y-3">
              <h4 class="text-xs font-bold uppercase tracking-wider text-surface-400">Inbound Request Telemetry</h4>
              <div class="grid grid-cols-2 gap-2 p-3 rounded-xl bg-surface-950 border border-surface-800 text-xs font-mono">
                <div>
                  <span class="text-surface-500 block text-[10px] uppercase font-sans">Method & Path</span>
                  <span class="text-white">{{ selectedExecution()!.httpMethod }} {{ selectedExecution()!.path }}</span>
                </div>
                <div>
                  <span class="text-surface-500 block text-[10px] uppercase font-sans">Client IP</span>
                  <span class="text-surface-300">{{ selectedExecution()!.clientIp || '127.0.0.1' }}</span>
                </div>
              </div>

              <!-- Inbound Headers -->
              <div>
                <span class="text-xs font-semibold text-surface-300 block mb-1">Request Headers</span>
                <app-code-viewer [code]="selectedExecution()!.headersJson" language="json"></app-code-viewer>
              </div>

              <!-- Inbound Body -->
              @if (selectedExecution()!.body) {
                <div>
                  <span class="text-xs font-semibold text-surface-300 block mb-1">Request Payload</span>
                  <app-code-viewer [code]="selectedExecution()!.body!" language="json"></app-code-viewer>
                </div>
              }
            </div>

            <!-- Simulated Response Details -->
            <div class="space-y-3 pt-4 border-t border-surface-800">
              <h4 class="text-xs font-bold uppercase tracking-wider text-amber-400">Simulated Outbound Response</h4>

              @if (selectedExecution()!.simulatedHeadersJson) {
                <div>
                  <span class="text-xs font-semibold text-surface-300 block mb-1">Simulated Response Headers</span>
                  <app-code-viewer [code]="selectedExecution()!.simulatedHeadersJson!" language="json"></app-code-viewer>
                </div>
              }

              @if (selectedExecution()!.simulatedResponseBody) {
                <div>
                  <span class="text-xs font-semibold text-surface-300 block mb-1">Simulated Response Body</span>
                  <app-code-viewer [code]="selectedExecution()!.simulatedResponseBody!" language="json"></app-code-viewer>
                </div>
              }
            </div>
          </div>
        </div>
      }

      <!-- CREATE / EDIT RULE MODAL -->
      @if (showRuleModal()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div class="fixed inset-0 bg-black/70 backdrop-blur-xs" (click)="closeRuleModal()"></div>
          <div class="relative w-full max-w-lg bg-surface-900 border border-surface-800 rounded-2xl shadow-2xl p-6 z-10 space-y-4 max-h-[90vh] overflow-y-auto">
            <div class="flex items-center justify-between pb-3 border-b border-surface-800">
              <h3 class="text-base font-bold text-white">{{ isEditing() ? 'Edit Chaos Rule' : 'Create Chaos Rule' }}</h3>
              <button (click)="closeRuleModal()" class="text-surface-400 hover:text-white">
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
                </svg>
              </button>
            </div>

            <div class="space-y-3.5 text-xs">
              <!-- Name -->
              <div class="space-y-1">
                <label class="font-semibold text-surface-300">Rule Name *</label>
                <input
                  type="text"
                  [(ngModel)]="ruleForm.name"
                  placeholder="e.g. Payment Gateway 429 Throttle"
                  class="w-full px-3.5 py-2.5 rounded-xl bg-surface-950 border border-surface-800 text-white placeholder-surface-600 focus:outline-none focus:border-amber-500"
                />
              </div>

              <!-- Custom Slug (only on create) -->
              @if (!isEditing()) {
                <div class="space-y-1">
                  <label class="font-semibold text-surface-300">Slug Identifier (optional)</label>
                  <input
                    type="text"
                    [(ngModel)]="ruleForm.slug"
                    placeholder="e.g. payment-throttle"
                    class="w-full px-3.5 py-2.5 rounded-xl bg-surface-950 border border-surface-800 text-white placeholder-surface-600 font-mono focus:outline-none focus:border-amber-500"
                  />
                </div>
              }

              <!-- Description -->
              <div class="space-y-1">
                <label class="font-semibold text-surface-300">Description</label>
                <input
                  type="text"
                  [(ngModel)]="ruleForm.description"
                  placeholder="e.g. Injects HTTP 429 with 30s retry-after header"
                  class="w-full px-3.5 py-2.5 rounded-xl bg-surface-950 border border-surface-800 text-white placeholder-surface-600 focus:outline-none focus:border-amber-500"
                />
              </div>

              <!-- Strategy -->
              <div class="space-y-1">
                <label class="font-semibold text-surface-300">Simulation Strategy</label>
                <select
                  [(ngModel)]="ruleForm.strategy"
                  class="w-full px-3.5 py-2.5 rounded-xl bg-surface-950 border border-surface-800 text-white focus:outline-none focus:border-amber-500 font-semibold">
                  <option value="FixedStatus">Fixed Status Code (Deterministic)</option>
                  <option value="FailureRate">Failure Rate % (Chaos Flakiness)</option>
                  <option value="SequentialRetryPattern">Sequential Retry Backoff (Fail N times then succeed)</option>
                  <option value="Timeout">Timeout & Heavy Latency</option>
                  <option value="ChaosJitter">Chaos Jitter & Random Fault Pool</option>
                  <option value="MalformedJson">Malformed / Corrupted JSON</option>
                </select>
              </div>

              <!-- Target Status Code -->
              <div class="grid grid-cols-2 gap-3">
                <div class="space-y-1">
                  <label class="font-semibold text-surface-300">Target Error Code</label>
                  <select
                    [(ngModel)]="ruleForm.targetStatusCode"
                    class="w-full px-3 py-2 rounded-xl bg-surface-950 border border-surface-800 text-white focus:outline-none focus:border-amber-500 font-mono">
                    <option [ngValue]="429">429 Too Many Requests</option>
                    <option [ngValue]="500">500 Internal Server Error</option>
                    <option [ngValue]="502">502 Bad Gateway</option>
                    <option [ngValue]="503">503 Service Unavailable</option>
                    <option [ngValue]="504">504 Gateway Timeout</option>
                    <option [ngValue]="400">400 Bad Request</option>
                    <option [ngValue]="401">401 Unauthorized</option>
                    <option [ngValue]="403">403 Forbidden</option>
                    <option [ngValue]="404">404 Not Found</option>
                    <option [ngValue]="422">422 Unprocessable</option>
                    <option [ngValue]="200">200 OK</option>
                  </select>
                </div>

                <div class="space-y-1">
                  <label class="font-semibold text-surface-300">Delay / Latency (ms)</label>
                  <input
                    type="number"
                    [(ngModel)]="ruleForm.delayMs"
                    min="0"
                    max="60000"
                    class="w-full px-3 py-2 rounded-xl bg-surface-950 border border-surface-800 text-white font-mono focus:outline-none focus:border-amber-500"
                  />
                </div>
              </div>

              <!-- Failure Rate Slider (if FailureRate) -->
              @if (ruleForm.strategy === 'FailureRate') {
                <div class="space-y-1 p-3 rounded-xl bg-surface-950 border border-surface-800">
                  <div class="flex justify-between items-center">
                    <label class="font-semibold text-surface-300">Failure Probability</label>
                    <span class="text-rose-400 font-mono font-bold">{{ ruleForm.failureRatePercent }}%</span>
                  </div>
                  <input
                    type="range"
                    [(ngModel)]="ruleForm.failureRatePercent"
                    min="0"
                    max="100"
                    step="5"
                    class="w-full accent-rose-500"
                  />
                </div>
              }

              <!-- Sequential Step Count (if SequentialRetryPattern) -->
              @if (ruleForm.strategy === 'SequentialRetryPattern') {
                <div class="space-y-1 p-3 rounded-xl bg-surface-950 border border-surface-800">
                  <label class="font-semibold text-surface-300">Fail First N Attempts Before 200 OK</label>
                  <input
                    type="number"
                    [(ngModel)]="ruleForm.failureStepCount"
                    min="1"
                    max="20"
                    class="w-full px-3 py-2 rounded-xl bg-surface-900 border border-surface-800 text-white font-mono focus:outline-none focus:border-amber-500"
                  />
                </div>
              }

              <!-- Custom Response Headers -->
              <div class="space-y-1">
                <label class="font-semibold text-surface-300">Custom Response Headers (JSON)</label>
                <input
                  type="text"
                  [(ngModel)]="ruleForm.responseHeadersJson"
                  placeholder='{"Retry-After": "30", "X-RateLimit-Limit": "100"}'
                  class="w-full px-3.5 py-2 rounded-xl bg-surface-950 border border-surface-800 text-white placeholder-surface-600 font-mono focus:outline-none focus:border-amber-500"
                />
              </div>

              <!-- Custom Response Body -->
              <div class="space-y-1">
                <label class="font-semibold text-surface-300">Custom Response Body (JSON)</label>
                <textarea
                  [(ngModel)]="ruleForm.responseBody"
                  rows="3"
                  placeholder='{"error": "Too Many Requests", "retry_after": 30}'
                  class="w-full p-2.5 rounded-xl bg-surface-950 border border-surface-800 text-white placeholder-surface-600 font-mono focus:outline-none focus:border-amber-500 resize-none">
                </textarea>
              </div>
            </div>

            <!-- Submit -->
            <div class="flex items-center justify-end gap-3 pt-3 border-t border-surface-800">
              <button
                (click)="closeRuleModal()"
                class="px-4 py-2 rounded-xl bg-surface-800 hover:bg-surface-700 text-surface-300 hover:text-white text-xs font-semibold">
                Cancel
              </button>
              <button
                (click)="saveRule()"
                [disabled]="savingRule() || !ruleForm.name"
                class="px-5 py-2 rounded-xl bg-amber-600 hover:bg-amber-500 text-white text-xs font-bold shadow-lg shadow-amber-600/30 transition-all disabled:opacity-50">
                {{ savingRule() ? 'Saving...' : (isEditing() ? 'Update Rule' : 'Create Rule') }}
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class SimulatorComponent implements OnInit, OnDestroy {
  private readonly destroyRef = inject(DestroyRef);
  private readonly simulatorService = inject(SimulatorService);
  readonly signalR = inject(SignalRService);
  private readonly toast = inject(ToastService);

  // Active Tab
  readonly activeTab = signal<'rules' | 'executions' | 'workbench'>('rules');

  // State
  readonly rules = signal<SimulatorRule[]>([]);
  readonly executions = signal<SimulatorExecution[]>([]);
  readonly totalExecutionsCount = signal<number>(0);
  readonly stats = signal<SimulatorStats | null>(null);

  readonly loadingRules = signal<boolean>(false);
  readonly loadingExecutions = signal<boolean>(false);
  readonly savingRule = signal<boolean>(false);
  readonly dispatchingTest = signal<boolean>(false);
  readonly isStreamPaused = signal<boolean>(false);

  // Filters & Selected Execution
  executionSearchTerm = '';
  executionStatusFilter: number | null = null;
  readonly selectedExecution = signal<SimulatorExecution | null>(null);

  // Modals & Forms
  readonly showRuleModal = signal<boolean>(false);
  readonly isEditing = signal<boolean>(false);
  readonly editingRuleId = signal<string | null>(null);

  ruleForm = {
    name: '',
    slug: '',
    description: '',
    strategy: 'FixedStatus' as SimulatorStrategy,
    targetStatusCode: 429,
    successStatusCode: 200,
    failureRatePercent: 50,
    failureStepCount: 3,
    delayMs: 0,
    responseHeadersJson: '{"Retry-After": "30"}',
    responseBody: '{"error": "Too Many Requests", "retry_after": 30}',
    isActive: true
  };

  // Workbench form
  workbenchTarget = 'ad-hoc-429';
  workbenchMethod = 'POST';
  workbenchEventHeader = 'payment.intent_failed';
  workbenchBody = '{\n  "event": "payment.failed",\n  "amount": 4900,\n  "currency": "usd",\n  "failure_reason": "card_declined"\n}';
  readonly workbenchResult = signal<SimulatedExecutionResult | null>(null);

  // Quick Presets
  readonly quickPresets: QuickPreset[] = [
    {
      name: '429 Rate Limit',
      badge: '429',
      color: 'bg-amber-500/20 text-amber-300 border-amber-500/30',
      description: 'Injects Retry-After: 30s',
      urlPath: '/api/v1/simulator/http/429?retryAfter=30',
      statusCode: 429,
      retryAfter: 30
    },
    {
      name: '500 Server Error',
      badge: '500',
      color: 'bg-rose-500/20 text-rose-300 border-rose-500/30',
      description: 'Simulates crash / outage',
      urlPath: '/api/v1/simulator/http/500',
      statusCode: 500
    },
    {
      name: '503 Unavailable',
      badge: '503',
      color: 'bg-rose-500/20 text-rose-300 border-rose-500/30',
      description: 'Backoff retry test',
      urlPath: '/api/v1/simulator/http/503',
      statusCode: 503
    },
    {
      name: '504 Timeout',
      badge: '504',
      color: 'bg-indigo-500/20 text-indigo-300 border-indigo-500/30',
      description: '5s simulated delay',
      urlPath: '/api/v1/simulator/timeout?delay=5000',
      statusCode: 504,
      delayMs: 5000
    },
    {
      name: 'Chaos Flakiness',
      badge: '50%',
      color: 'bg-purple-500/20 text-purple-300 border-purple-500/30',
      description: '50% failure probability',
      urlPath: '/api/v1/simulator/chaos?failureRate=50&failStatus=500',
      statusCode: 500
    },
    {
      name: '200 OK',
      badge: '200',
      color: 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30',
      description: 'Standard success',
      urlPath: '/api/v1/simulator/http/200',
      statusCode: 200
    }
  ];

  constructor() {
    // Reactive SignalR listener for live simulator execution events
    effect(() => {
      const evt = this.signalR.latestSimulatorEvent();
      if (evt && !this.isStreamPaused()) {
        this.executions.update(prev => [evt.execution, ...prev.slice(0, 99)]);
        this.totalExecutionsCount.update(c => c + 1);
        this.loadStats();
      }
    });

    effect(() => {
      const cleared = this.signalR.clearedSimulatorRuleId();
      if (cleared) {
        this.loadExecutions();
        this.loadStats();
      }
    });
  }

  ngOnInit(): void {
    this.refreshAll();
  }

  ngOnDestroy(): void {}

  refreshAll(): void {
    this.loadRules();
    this.loadExecutions();
    this.loadStats();
  }

  loadRules(): void {
    this.loadingRules.set(true);
    this.simulatorService.getRules().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: data => {
        this.rules.set(data);
        this.loadingRules.set(false);
      },
      error: () => this.loadingRules.set(false)
    });
  }

  loadExecutions(): void {
    this.loadingExecutions.set(true);
    this.simulatorService.getExecutions({
      search: this.executionSearchTerm || undefined,
      statusCode: this.executionStatusFilter ?? undefined,
      pageSize: 50
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: data => {
        this.executions.set(data.items);
        this.totalExecutionsCount.set(data.totalCount);
        this.loadingExecutions.set(false);
      },
      error: () => this.loadingExecutions.set(false)
    });
  }

  loadStats(): void {
    this.simulatorService.getStats().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: data => this.stats.set(data),
      error: () => {}
    });
  }

  applyExecutionFilters(): void {
    this.loadExecutions();
  }

  toggleStreamPause(): void {
    this.isStreamPaused.update(p => !p);
  }

  selectExecution(exec: SimulatorExecution): void {
    this.selectedExecution.set(exec);
  }

  clearExecutions(): void {
    this.simulatorService.clearExecutions().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.executions.set([]);
        this.totalExecutionsCount.set(0);
        this.loadStats();
        this.toast.success('Logs Cleared', 'Simulator execution history has been cleared.');
      }
    });
  }

  resetRuleSteps(ruleId: string): void {
    this.simulatorService.resetRuleSteps(ruleId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.loadRules();
        this.toast.success('Step Counter Reset', 'Rule sequential step counter has been reset to 0.');
      }
    });
  }

  deleteRule(ruleId: string): void {
    if (!confirm('Are you sure you want to delete this simulator rule?')) return;

    this.simulatorService.deleteRule(ruleId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.loadRules();
        this.loadStats();
        this.toast.success('Rule Deleted', 'Simulator rule removed.');
      }
    });
  }

  openCreateModal(): void {
    this.isEditing.set(false);
    this.editingRuleId.set(null);
    this.ruleForm = {
      name: '',
      slug: '',
      description: '',
      strategy: 'FixedStatus',
      targetStatusCode: 429,
      successStatusCode: 200,
      failureRatePercent: 50,
      failureStepCount: 3,
      delayMs: 0,
      responseHeadersJson: '{"Retry-After": "30"}',
      responseBody: '{"error": "Too Many Requests", "retry_after": 30}',
      isActive: true
    };
    this.showRuleModal.set(true);
  }

  openEditModal(rule: SimulatorRule): void {
    this.isEditing.set(true);
    this.editingRuleId.set(rule.id);
    this.ruleForm = {
      name: rule.name,
      slug: rule.slug,
      description: rule.description ?? '',
      strategy: (typeof rule.strategy === 'string' ? rule.strategy : rule.strategyName) as SimulatorStrategy,
      targetStatusCode: rule.targetStatusCode,
      successStatusCode: rule.successStatusCode,
      failureRatePercent: rule.failureRatePercent,
      failureStepCount: rule.failureStepCount,
      delayMs: rule.delayMs,
      responseHeadersJson: rule.responseHeadersJson ?? '',
      responseBody: rule.responseBody ?? '',
      isActive: rule.isActive
    };
    this.showRuleModal.set(true);
  }

  closeRuleModal(): void {
    this.showRuleModal.set(false);
  }

  saveRule(): void {
    if (!this.ruleForm.name) return;
    this.savingRule.set(true);

    if (this.isEditing() && this.editingRuleId()) {
      const updateReq: UpdateSimulatorRuleRequest = {
        name: this.ruleForm.name,
        description: this.ruleForm.description || null,
        strategy: this.ruleForm.strategy,
        targetStatusCode: Number(this.ruleForm.targetStatusCode),
        successStatusCode: Number(this.ruleForm.successStatusCode),
        failureRatePercent: Number(this.ruleForm.failureRatePercent),
        failureStepCount: Number(this.ruleForm.failureStepCount),
        delayMs: Number(this.ruleForm.delayMs),
        minDelayMs: 0,
        maxDelayMs: 0,
        responseHeadersJson: this.ruleForm.responseHeadersJson || null,
        responseBody: this.ruleForm.responseBody || null,
        responseContentType: 'application/json',
        isActive: this.ruleForm.isActive
      };

      this.simulatorService.updateRule(this.editingRuleId()!, updateReq).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.savingRule.set(false);
          this.closeRuleModal();
          this.loadRules();
          this.toast.success('Rule Updated', 'Chaos simulator rule updated successfully.');
        },
        error: () => this.savingRule.set(false)
      });
    } else {
      const createReq: CreateSimulatorRuleRequest = {
        name: this.ruleForm.name,
        slug: this.ruleForm.slug || null,
        description: this.ruleForm.description || null,
        strategy: this.ruleForm.strategy,
        targetStatusCode: Number(this.ruleForm.targetStatusCode),
        successStatusCode: Number(this.ruleForm.successStatusCode),
        failureRatePercent: Number(this.ruleForm.failureRatePercent),
        failureStepCount: Number(this.ruleForm.failureStepCount),
        delayMs: Number(this.ruleForm.delayMs),
        minDelayMs: 0,
        maxDelayMs: 0,
        responseHeadersJson: this.ruleForm.responseHeadersJson || null,
        responseBody: this.ruleForm.responseBody || null,
        responseContentType: 'application/json'
      };

      this.simulatorService.createRule(createReq).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.savingRule.set(false);
          this.closeRuleModal();
          this.loadRules();
          this.loadStats();
          this.toast.success('Rule Created', 'New chaos simulator rule created.');
        },
        error: () => this.savingRule.set(false)
      });
    }
  }

  triggerWorkbenchTest(rule: SimulatorRule): void {
    this.workbenchTarget = rule.id;
    this.activeTab.set('workbench');
  }

  executeWorkbenchDispatch(): void {
    this.dispatchingTest.set(true);

    let req: TestDispatchRequest;
    if (this.workbenchTarget.startsWith('ad-hoc-')) {
      const type = this.workbenchTarget.replace('ad-hoc-', '');
      if (type === '429') {
        req = { adHocStatusCode: 429, adHocRetryAfter: 30, adHocDelayMs: 50 };
      } else if (type === '500') {
        req = { adHocStatusCode: 500 };
      } else if (type === '503') {
        req = { adHocStatusCode: 503 };
      } else if (type === 'timeout') {
        req = { adHocStatusCode: 504, adHocDelayMs: 1500 };
      } else if (type === 'chaos') {
        req = { adHocStatusCode: 500, adHocFailureRate: 50 };
      } else {
        req = { adHocStatusCode: 200 };
      }
    } else {
      req = { ruleId: this.workbenchTarget };
    }

    req.httpMethod = this.workbenchMethod;
    req.payloadBody = this.workbenchBody;
    req.headersJson = JSON.stringify({
      'Content-Type': 'application/json',
      'X-Webhook-Event': this.workbenchEventHeader,
      'User-Agent': 'HookBridge-SimulatorWorkbench/1.0'
    });

    this.simulatorService.testDispatch(req).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        this.workbenchResult.set(res);
        this.dispatchingTest.set(false);
        this.toast.success('Test Executed', `Simulated HTTP ${res.statusCode} (${res.injectedFault})`);
      },
      error: () => this.dispatchingTest.set(false)
    });
  }

  loadSamplePayload(): void {
    this.workbenchBody = JSON.stringify({
      event: 'payment.disputed',
      timestamp: new Date().toISOString(),
      dispute: {
        id: 'dp_998124',
        amount: 8500,
        reason: 'fraudulent',
        status: 'under_review'
      }
    }, null, 2);
  }

  copyPresetUrl(preset: QuickPreset): void {
    const origin = window.location.origin;
    const fullUrl = `${origin}${preset.urlPath}`;
    this.copyToClipboard(fullUrl, `${preset.name} URL copied to clipboard`);
  }

  copyToClipboard(text: string, message: string): void {
    navigator.clipboard.writeText(text);
    this.toast.success('Copied to Clipboard', message);
  }

  formatJson(obj: any): string {
    try {
      return JSON.stringify(obj, null, 2);
    } catch {
      return '{}';
    }
  }

  getStatusCodeBadgeClass(code: number): string {
    if (code >= 200 && code < 300) return 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30';
    if (code === 429) return 'bg-amber-500/20 text-amber-300 border-amber-500/30';
    if (code >= 400 && code < 500) return 'bg-amber-500/20 text-amber-300 border-amber-500/30';
    return 'bg-rose-500/20 text-rose-300 border-rose-500/30';
  }

  getStrategyBadgeClass(strategy: any): string {
    const s = String(strategy);
    if (s.includes('Fixed') || s === '0') return 'bg-blue-500/10 text-blue-300 border-blue-500/20';
    if (s.includes('FailureRate') || s === '1') return 'bg-purple-500/10 text-purple-300 border-purple-500/20';
    if (s.includes('Sequential') || s === '2') return 'bg-amber-500/10 text-amber-300 border-amber-500/20';
    if (s.includes('Timeout') || s === '3') return 'bg-indigo-500/10 text-indigo-300 border-indigo-500/20';
    if (s.includes('Chaos') || s === '4') return 'bg-rose-500/10 text-rose-300 border-rose-500/20';
    return 'bg-surface-800 text-surface-300 border-surface-700';
  }
}
