import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="min-h-screen bg-surface-950 flex flex-col justify-center py-12 sm:px-6 lg:px-8 font-sans">
      <div class="sm:mx-auto sm:w-full sm:max-w-md text-center">
        <div class="inline-flex w-12 h-12 rounded-xl bg-gradient-to-tr from-brand-600 to-brand-400 items-center justify-center font-bold text-white text-lg shadow-xl shadow-brand-500/20 mb-4">
          HB
        </div>
        <h2 class="text-2xl font-bold tracking-tight text-white">Sign in to HookBridge</h2>
        <p class="mt-2 text-sm text-surface-400">
          Webhook Gateway & Developer Observability Platform
        </p>
      </div>

      <div class="mt-8 sm:mx-auto sm:w-full sm:max-w-md">
        <div class="bg-surface-900/90 border border-surface-800 py-8 px-4 shadow-2xl rounded-xl sm:px-10">
          @if (errorMessage()) {
            <div class="mb-4 p-3 rounded-lg bg-rose-950/50 border border-rose-800/80 text-rose-300 text-xs flex items-center gap-2">
              <svg class="w-4 h-4 shrink-0 text-rose-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
              </svg>
              <span>{{ errorMessage() }}</span>
            </div>
          }

          <form [formGroup]="loginForm" (ngSubmit)="onSubmit()" class="space-y-4">
            <div>
              <label for="email" class="block text-xs font-medium text-surface-300">Email address</label>
              <input id="email" type="email" formControlName="email"
                     class="mt-1 block w-full px-3 py-2 bg-surface-950 border border-surface-700 rounded-lg text-sm text-white focus:outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500 transition-colors"
                     placeholder="developer@company.com" />
            </div>

            <div>
              <label for="password" class="block text-xs font-medium text-surface-300">Password</label>
              <input id="password" type="password" formControlName="password"
                     class="mt-1 block w-full px-3 py-2 bg-surface-950 border border-surface-700 rounded-lg text-sm text-white focus:outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500 transition-colors"
                     placeholder="••••••••••••" />
            </div>

            <div>
              <label for="tenantIdentifier" class="block text-xs font-medium text-surface-300">
                Tenant Slug <span class="text-surface-500 text-[11px]">(Optional)</span>
              </label>
              <input id="tenantIdentifier" type="text" formControlName="tenantIdentifier"
                     class="mt-1 block w-full px-3 py-2 bg-surface-950 border border-surface-700 rounded-lg text-sm text-white font-mono focus:outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500 transition-colors"
                     placeholder="acme-corp" />
            </div>

            <div class="pt-2">
              <button type="submit" [disabled]="loginForm.invalid || isLoading()"
                      class="w-full flex justify-center py-2.5 px-4 border border-transparent rounded-lg shadow-sm text-sm font-medium text-white bg-brand-600 hover:bg-brand-500 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-brand-500 disabled:opacity-50 disabled:cursor-not-allowed transition-all">
                @if (isLoading()) {
                  <span class="inline-block animate-spin mr-2">⏳</span> Signing in...
                } @else {
                  Sign in
                }
              </button>
            </div>
          </form>

          <div class="mt-6 text-center border-t border-surface-800 pt-4">
            <p class="text-xs text-surface-400">
              Need a new organization?
              <a routerLink="/auth/register" class="font-medium text-brand-400 hover:text-brand-300 transition-colors ml-1">
                Register tenant
              </a>
            </p>
          </div>
        </div>
      </div>
    </div>
  `
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly loginForm = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]],
    tenantIdentifier: ['']
  });

  onSubmit(): void {
    if (this.loginForm.invalid) return;

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const { email, password, tenantIdentifier } = this.loginForm.value;

    this.auth.login({
      email: email!,
      password: password!,
      tenantIdentifier: tenantIdentifier ? tenantIdentifier.trim() : null
    }).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err.error?.detail || err.error?.title || 'Invalid credentials or tenant.');
      }
    });
  }
}
