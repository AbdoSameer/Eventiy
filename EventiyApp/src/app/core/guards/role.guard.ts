import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { UserRole } from '../models/auth.model';
import { AuthApplicationService } from '../../application/services/auth-application.service';

/**
 * Functional role guard factory.
 *
 * Usage in route config:
 *   canActivate: [roleGuard(['Organizer', 'Admin'])]
 *
 * Returns true if the current user's role is in the allow-list, otherwise
 * redirects to /unauthorized.
 */
export function roleGuard(allowedRoles: UserRole[]): CanActivateFn {
  return () => {
    const authService = inject(AuthApplicationService);
    const router = inject(Router);
    const currentRole = authService.userRole();

    if (currentRole && allowedRoles.includes(currentRole)) {
      return true;
    }
    return router.createUrlTree(['/unauthorized']);
  };
}
