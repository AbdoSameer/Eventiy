import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { ReplaySubject, catchError, finalize, switchMap, take, tap, throwError } from 'rxjs';
import { AuthApplicationService } from '../../application/services/auth-application.service';

const ANONYMOUS_ENDPOINTS = [
  '/api/auth/login',
  '/api/auth/register',
];

const REFRESH_ENDPOINT = '/api/auth/refresh';

let isRefreshing = false;
let refreshResult = new ReplaySubject<string | null>(1);

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthApplicationService);
  const token = authService.getToken();

  if (token && !ANONYMOUS_ENDPOINTS.some(e => req.url.endsWith(e))) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && !req.url.endsWith(REFRESH_ENDPOINT)) {
        if (!isRefreshing) {
          isRefreshing = true;
          refreshResult = new ReplaySubject<string | null>(1);
          authService.refreshToken().pipe(
            tap(token => {
              refreshResult.next(token);
              refreshResult.complete();
            }),
            finalize(() => { isRefreshing = false; }),
            catchError(() => {
              refreshResult.next(null);
              refreshResult.complete();
              return throwError(() => err);
            }),
          ).subscribe();
        }
        return refreshResult.pipe(
          take(1),
          switchMap(newToken => {
            if (!newToken) return throwError(() => err);
            const newReq = req.clone({
              setHeaders: { Authorization: `Bearer ${newToken}` },
            });
            return next(newReq);
          }),
        );
      }
      return throwError(() => err);
    }),
  );
};