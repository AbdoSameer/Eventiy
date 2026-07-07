import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthApplicationService } from '../../application/services/auth-application.service';

/**
 * Functional auth guard.
 *
 * Allows activation when authenticated; otherwise redirects to /login while
 * preserving the attempted URL as `returnUrl` so we can bounce back after login.
 */
export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthApplicationService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }
  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url },
  });
};
