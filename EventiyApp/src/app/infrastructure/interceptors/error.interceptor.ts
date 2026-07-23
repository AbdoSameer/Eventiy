import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthApplicationService } from '../../application/services/auth-application.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthApplicationService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 403) {
        authService.navigateToUnauthorized();
      }

      return throwError(() => error);
    }),
  );
};
