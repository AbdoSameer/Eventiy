import { HttpErrorResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { AuthResponse, LoginRequest, RegisterRequest } from '../../core/models/auth.model';
import { Result } from '../../core/models/result.model';
import { HttpClientBase } from './http-client-base';

@Injectable({ providedIn: 'root' })
export class AuthHttpService extends HttpClientBase {
  login(credentials: LoginRequest): Observable<Result<AuthResponse>> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/login`, credentials).pipe(
      map((value): Result<AuthResponse> => ({ isSuccess: true, isFailure: false, value })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  register(data: RegisterRequest): Observable<Result<AuthResponse>> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/register`, data).pipe(
      map((value): Result<AuthResponse> => ({ isSuccess: true, isFailure: false, value })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  refresh(): Observable<Result<AuthResponse>> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/refresh`, {}).pipe(
      map((value): Result<AuthResponse> => ({ isSuccess: true, isFailure: false, value })),
      catchError((err) => this.toErrorResult(err)),
    );
  }
}
