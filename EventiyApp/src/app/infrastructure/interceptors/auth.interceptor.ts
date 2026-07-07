import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthApplicationService } from '../../application/services/auth-application.service';

/**
 * Functional auth interceptor.
 *
 * Attaches `Authorization: Bearer {token}` to every outgoing request except
 * those hitting the `/auth/` endpoints (login/register), which must remain
 * unauthenticated so the backend can issue the initial token.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthApplicationService);
  const token = authService.getToken();

  if (token && !req.url.includes('/auth/')) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }
  return next(req);
};
