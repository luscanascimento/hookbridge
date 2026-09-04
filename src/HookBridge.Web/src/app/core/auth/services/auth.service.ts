import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { AuthResponse, LoginCredentials, RegisterData, UserProfile } from '../models/auth.models';
import { environment } from '../../../../environments/environment';

const ACCESS_TOKEN_KEY = 'hb_access_token';
const REFRESH_TOKEN_KEY = 'hb_refresh_token';
const USER_PROFILE_KEY = 'hb_user_profile';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  // Signal-based reactive state
  readonly currentUser = signal<UserProfile | null>(null);
  readonly token = signal<string | null>(null);
  readonly refreshToken = signal<string | null>(null);

  readonly isAuthenticated = computed(() => !!this.currentUser() && !!this.token());
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
      const storedToken = localStorage.getItem(ACCESS_TOKEN_KEY);
      const storedRefresh = localStorage.getItem(REFRESH_TOKEN_KEY);
      const storedUser = localStorage.getItem(USER_PROFILE_KEY);

      if (storedToken && storedUser) {
        this.token.set(storedToken);
        this.refreshToken.set(storedRefresh);
        this.currentUser.set(JSON.parse(storedUser) as UserProfile);
      }
    } catch {
      this.clearStorage();
    }
  }

  login(credentials: LoginCredentials): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiBaseUrl}/auth/login`, credentials).pipe(
      tap(response => this.handleAuthSuccess(response))
    );
  }

  register(data: RegisterData): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiBaseUrl}/auth/register`, data).pipe(
      tap(response => this.handleAuthSuccess(response))
    );
  }

  refresh(): Observable<AuthResponse> {
    const currentRefresh = this.refreshToken();
    return this.http.post<AuthResponse>(`${environment.apiBaseUrl}/auth/refresh`, {
      refreshToken: currentRefresh
    }).pipe(
      tap(response => this.handleAuthSuccess(response))
    );
  }

  logout(): void {
    this.clearStorage();
    this.currentUser.set(null);
    this.token.set(null);
    this.refreshToken.set(null);
    this.router.navigate(['/auth/login']);
  }

  getAccessToken(): string | null {
    return this.token();
  }

  private handleAuthSuccess(response: AuthResponse): void {
    this.token.set(response.accessToken);
    this.refreshToken.set(response.refreshToken);
    this.currentUser.set(response.user);

    try {
      localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
      localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
      localStorage.setItem(USER_PROFILE_KEY, JSON.stringify(response.user));
    } catch {
      // Storage unavailable or quota exceeded
    }
  }

  private clearStorage(): void {
    try {
      localStorage.removeItem(ACCESS_TOKEN_KEY);
      localStorage.removeItem(REFRESH_TOKEN_KEY);
      localStorage.removeItem(USER_PROFILE_KEY);
    } catch {
      // Ignored
    }
  }
}
