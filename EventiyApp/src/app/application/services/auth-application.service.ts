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

  private readonly tokenSignal = signal<string | null>(null);

  readonly currentUser = signal<AuthResponse | null>(null);
  readonly isAuthenticated = computed(() => this.tokenSignal() !== null);
  readonly userRole = computed(() => this.currentUser()?.role ?? null);

  constructor() {
    this.tryRestoreSession();
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
    this.tokenSignal.set(null);
    this.currentUser.set(null);
    this.router.navigateByUrl('/');
  }

  navigateToUnauthorized(): void {
    this.router.navigateByUrl('/unauthorized');
  }

  getToken(): string | null {
    return this.tokenSignal();
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

  private applySession(response: AuthResponse): void {
    this.tokenSignal.set(response.token);
    this.currentUser.set(response);
  }

  private tryRestoreSession(): void {
    this.authHttp.refresh().pipe(
      catchError(() => of({ isSuccess: false, isFailure: true, errors: [] } as Result<AuthResponse>)),
    ).subscribe(result => {
      if (result.isSuccess && result.value?.token) {
        this.applySession(result.value);
      }
    });
  }
}
