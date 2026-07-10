import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthApplicationService } from '../../application/services/auth-application.service';

const ANONYMOUS_ENDPOINTS = [
  '/api/auth/login',
  '/api/auth/register',
];

/**
 * Attaches `Authorization: Bearer {token}` to every outgoing request except
 * those in the explicit anonymous endpoint whitelist (login/register).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthApplicationService);
  const token = authService.getToken();

  if (token && !ANONYMOUS_ENDPOINTS.some(e => req.url.includes(e))) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }
  return next(req);
};
