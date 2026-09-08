import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/auth/services/auth.service';
import { LiveIndicatorComponent } from '../ui/live-indicator.component';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, LiveIndicatorComponent],
  template: `
    <div class="flex h-screen bg-surface-950 text-surface-100 overflow-hidden font-sans">
      <!-- Sidebar -->
      <aside class="w-64 bg-surface-900/90 border-r border-surface-800 flex flex-col justify-between shrink-0">
        <div>
          <!-- Brand Logo -->
          <div class="h-16 flex items-center px-6 border-b border-surface-800/80 gap-3">
            <div class="w-8 h-8 rounded-lg bg-gradient-to-tr from-brand-600 to-brand-400 flex items-center justify-center font-bold text-white shadow-lg shadow-brand-500/20">
              HB
            </div>
            <div>
              <div class="font-bold text-sm tracking-tight text-white flex items-center gap-1.5">
                HookBridge
                <span class="text-[10px] bg-brand-500/20 text-brand-300 font-mono px-1.5 py-0.2 rounded border border-brand-500/30">v1.0</span>
              </div>
              <div class="text-[11px] text-surface-400 font-mono tracking-tight truncate max-w-[120px]">
                {{ auth.tenantIdentifier() || 'Default Tenant' }}
              </div>
            </div>
          </div>

          <!-- Navigation Links -->
          <nav class="p-3 space-y-1">
            <a routerLink="/dashboard" routerLinkActive="bg-brand-600/15 text-brand-300 border-brand-500/40 font-medium"
               class="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-surface-300 hover:bg-surface-800 hover:text-white transition-colors border border-transparent">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6"/>
              </svg>
              <span>Dashboard</span>
            </a>

            <a routerLink="/live" routerLinkActive="bg-brand-600/15 text-brand-300 border-brand-500/40 font-medium"
               class="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-surface-300 hover:bg-surface-800 hover:text-white transition-colors border border-transparent">
              <span class="w-4 h-4 flex items-center justify-center">
                <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
              </span>
              <span>Live Inspector</span>
            </a>

            <a routerLink="/endpoints" routerLinkActive="bg-brand-600/15 text-brand-300 border-brand-500/40 font-medium"
               class="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-surface-300 hover:bg-surface-800 hover:text-white transition-colors border border-transparent">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/>
              </svg>
              <span>Endpoints</span>
            </a>

            <a routerLink="/deliveries" routerLinkActive="bg-brand-600/15 text-brand-300 border-brand-500/40 font-medium"
               class="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-surface-300 hover:bg-surface-800 hover:text-white transition-colors border border-transparent">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01"/>
              </svg>
              <span>Deliveries</span>
            </a>

            <a routerLink="/traces" routerLinkActive="bg-brand-600/15 text-brand-300 border-brand-500/40 font-medium"
               class="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-surface-300 hover:bg-surface-800 hover:text-white transition-colors border border-transparent">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/>
              </svg>
              <span>Trace Explorer</span>
            </a>

            <a routerLink="/payloads" routerLinkActive="bg-brand-600/15 text-brand-300 border-brand-500/40 font-medium"
               class="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-surface-300 hover:bg-surface-800 hover:text-white transition-colors border border-transparent">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4"/>
              </svg>
              <span>Payload Inspector</span>
            </a>

            <a routerLink="/schemas" routerLinkActive="bg-brand-600/15 text-brand-300 border-brand-500/40 font-medium"
               class="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-surface-300 hover:bg-surface-800 hover:text-white transition-colors border border-transparent">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>
              </svg>
              <span>Schemas</span>
            </a>

            <a routerLink="/docs" routerLinkActive="bg-brand-600/15 text-brand-300 border-brand-500/40 font-medium"
               class="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-surface-300 hover:bg-surface-800 hover:text-white transition-colors border border-transparent">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253"/>
              </svg>
              <span>Documentation</span>
            </a>

            <a routerLink="/sandbox" routerLinkActive="bg-brand-600/15 text-brand-300 border-brand-500/40 font-medium"
               class="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-surface-300 hover:bg-surface-800 hover:text-white transition-colors border border-transparent">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19.428 15.428a2 2 0 00-1.022-.547l-2.387-.477a6 6 0 00-3.86.517l-.318.158a6 6 0 01-3.86.517L6.05 15.21a2 2 0 00-1.806.547M8 4h8l-1 1v5.172a2 2 0 00.586 1.414l5 5c1.26 1.26.367 3.414-1.415 3.414H4.828c-1.782 0-2.674-2.154-1.414-3.414l5-5A2 2 0 009 10.172V5L8 4z"/>
              </svg>
              <span>Sandbox</span>
            </a>

            <a routerLink="/simulator" routerLinkActive="bg-brand-600/15 text-brand-300 border-brand-500/40 font-medium"
               class="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-surface-300 hover:bg-surface-800 hover:text-white transition-colors border border-transparent">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/>
              </svg>
              <span>Simulator</span>
            </a>

            <div class="pt-3 pb-1 px-3 text-[11px] font-semibold text-surface-500 uppercase tracking-wider">
              Management
            </div>

            <a routerLink="/api-keys" routerLinkActive="bg-brand-600/15 text-brand-300 border-brand-500/40 font-medium"
               class="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-surface-300 hover:bg-surface-800 hover:text-white transition-colors border border-transparent">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z"/>
              </svg>
              <span>API Keys</span>
            </a>

            <a routerLink="/audit" routerLinkActive="bg-brand-600/15 text-brand-300 border-brand-500/40 font-medium"
               class="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-surface-300 hover:bg-surface-800 hover:text-white transition-colors border border-transparent">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/>
              </svg>
              <span>Audit Logs</span>
            </a>
          </nav>
        </div>

        <!-- User profile footer -->
        <div class="p-3 border-t border-surface-800 bg-surface-950/40">
          <div class="flex items-center justify-between p-2 rounded-lg hover:bg-surface-800/50 transition-colors">
            <div class="flex flex-col min-w-0 pr-2">
              <span class="text-xs font-medium text-surface-200 truncate">{{ auth.currentUser()?.email }}</span>
              <span class="text-[10px] text-brand-400 font-mono">{{ auth.userRole() }}</span>
            </div>
            <button (click)="logout()" title="Logout"
                    class="p-1.5 text-surface-400 hover:text-rose-400 hover:bg-rose-950/30 rounded transition-colors">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"/>
              </svg>
            </button>
          </div>
        </div>
      </aside>

      <!-- Main Layout -->
      <div class="flex-1 flex flex-col min-w-0 overflow-hidden">
        <!-- Topbar -->
        <header class="h-16 bg-surface-900/50 border-b border-surface-800 flex items-center justify-between px-6 shrink-0 backdrop-blur-sm">
          <div class="flex items-center gap-3">
            <span class="text-xs font-mono bg-surface-800 text-surface-300 px-2 py-0.5 rounded border border-surface-700">
              Tenant ID: {{ auth.tenantId()?.slice(0, 8) }}...
            </span>
          </div>

          <div class="flex items-center gap-4">
            <app-live-indicator></app-live-indicator>
          </div>
        </header>

        <!-- Page Outlet Container -->
        <main class="flex-1 overflow-y-auto p-6 bg-surface-950">
          <router-outlet></router-outlet>
        </main>
      </div>
    </div>
  `
})
export class AppShellComponent {
  readonly auth = inject(AuthService);

  logout(): void {
    this.auth.logout();
  }
}
