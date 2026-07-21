import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { AuthHttpService } from '../http/auth.http-service';
import { AuthResponse, LoginRequest, RegisterRequest } from '../../core/models/auth.model';
import { Result } from '../../core/models/result.model';

@Injectable({ providedIn: 'root' })
export class AuthApplicationService {
  private readonly authHttp = inject(AuthHttpService);
  private readonly router = inject(Router);

  readonly currentUser = signal<AuthResponse | null>(null);
  readonly isAuthenticated = computed(() => this.currentUser() !== null && !!this.currentUser()?.token);
  readonly userRole = computed(() => this.currentUser()?.role ?? null);

  constructor() {
    this.restoreSession();
  }

  login(credentials: LoginRequest): Observable<Result<AuthResponse>> {
    return this.authHttp.login(credentials).pipe(
      map(result => {
        if (result.isSuccess && result.value) {
          this.applySession(result.value);
        }
        return result;
      }),
    );
  }

  register(data: RegisterRequest): Observable<Result<AuthResponse>> {
    return this.authHttp.register(data).pipe(
      map(result => {
        if (result.isSuccess && result.value) {
          if (result.value.requiresApproval || !result.value.token) {
            this.currentUser.set({ ...result.value, token: null, expiresAt: null });
            return { ...result, value: { ...result.value, token: null, expiresAt: null } };
          }
          this.applySession(result.value);
        }
        return result;
      }),
    );
  }

  logout(): void {
    this.currentUser.set(null);
    sessionStorage.removeItem('auth_user');
    this.router.navigateByUrl('/');
  }

  navigateToUnauthorized(): void {
    this.router.navigateByUrl('/unauthorized');
  }

  getToken(): string | null {
    return this.currentUser()?.token ?? null;
  }

  refreshToken(): Observable<string | null> {
    const current = this.currentUser();
    if (!current) return of(null);

    return this.authHttp.refresh().pipe(
      map(result => {
        if (result.isSuccess && result.value?.token) {
          this.applySession({ ...current, ...result.value });
          return result.value.token;
        }
        this.logout();
        return null;
      }),
      catchError(() => {
        this.logout();
        return of(null);
      }),
    );
  }

  /**
   * Persists the auth session into sessionStorage (not localStorage).
   *
   * sessionStorage is cleared when the tab is closed, which limits the
   * exposure window in case of XSS. The ideal long-term fix is to have
   * the backend issue the JWT via an HttpOnly cookie so the token is
   * never accessible to JavaScript at all.
   */
  private applySession(response: AuthResponse): void {
    this.currentUser.set(response);
    sessionStorage.setItem('auth_user', JSON.stringify(response));
  }

  private restoreSession(): void {
    try {
      const stored = sessionStorage.getItem('auth_user');
      if (stored) {
        const parsed = JSON.parse(stored) as AuthResponse;
        this.currentUser.set(parsed);
      }
    } catch {
      sessionStorage.removeItem('auth_user');
    }
  }
}
