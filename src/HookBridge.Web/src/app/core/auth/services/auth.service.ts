import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { AuthCookieResponse, LoginCredentials, RegisterData, UserProfile } from '../models/auth.models';
import { environment } from '../../../../environments/environment';

const USER_PROFILE_KEY = 'hb_user_profile';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  // Signal-based reactive state
  readonly currentUser = signal<UserProfile | null>(null);

  readonly isAuthenticated = computed(() => !!this.currentUser());
  readonly tenantId = computed(() => this.currentUser()?.tenantId ?? null);
  readonly tenantIdentifier = computed(() => this.currentUser()?.tenantIdentifier ?? '');
  readonly userRole = computed(() => this.currentUser()?.role ?? null);
  readonly isTenantAdmin = computed(() => {
    const role = this.userRole();
    return role === 'TenantAdmin' || role === 'SystemOperator';
  });
  readonly isDeveloper = computed(() => {
    const role = this.userRole();
    return role === 'Developer' || this.isTenantAdmin();
  });

  constructor() {
    this.initializeFromStorage();
  }

  private initializeFromStorage(): void {
    try {
      const storedUser = localStorage.getItem(USER_PROFILE_KEY);

      if (storedUser) {
        this.currentUser.set(JSON.parse(storedUser) as UserProfile);
      }
    } catch {
      this.clearStorage();
    }
  }

  login(credentials: LoginCredentials): Observable<AuthCookieResponse> {
    return this.http.post<AuthCookieResponse>(`${environment.apiBaseUrl}/auth/login`, credentials).pipe(
      tap(response => this.handleAuthSuccess(response))
    );
  }

  register(data: RegisterData): Observable<AuthCookieResponse> {
    return this.http.post<AuthCookieResponse>(`${environment.apiBaseUrl}/auth/register`, data).pipe(
      tap(response => this.handleAuthSuccess(response))
    );
  }

  refresh(): Observable<AuthCookieResponse> {
    return this.http.post<AuthCookieResponse>(`${environment.apiBaseUrl}/auth/refresh`, {}).pipe(
      tap(response => this.handleAuthSuccess(response))
    );
  }

  logout(): void {
    this.http.post(`${environment.apiBaseUrl}/auth/logout`, {}).subscribe({
      next: () => {},
      error: () => {} // Graceful fallback if offline or network failure
    });

    this.clearStorage();
    this.currentUser.set(null);
    this.router.navigate(['/auth/login']);
  }

  private handleAuthSuccess(response: AuthCookieResponse): void {
    this.currentUser.set(response.user);

    try {
      localStorage.setItem(USER_PROFILE_KEY, JSON.stringify(response.user));
    } catch {
      // Storage unavailable or quota exceeded
    }
  }

  private clearStorage(): void {
    try {
      localStorage.removeItem(USER_PROFILE_KEY);
    } catch {
      // Ignored
    }
  }
}
