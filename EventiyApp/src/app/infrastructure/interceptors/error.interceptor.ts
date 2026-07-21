import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthApplicationService } from '../../application/services/auth-application.service';
import { ToastService } from '../services/toast.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toastService = inject(ToastService);
  const authService = inject(AuthApplicationService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      const problem = error.error;
      const errors = problem?.errors as Array<{ code: string; message: string }> | undefined;

      switch (error.status) {
        case 400: {
          if (errors && errors.length > 0) {
            errors.forEach((e) => toastService.showError(e.message));
          } else if (problem?.detail) {
            toastService.showError(problem.detail);
          } else {
            toastService.showError(problem?.title ?? 'Invalid request. Please check your input.');
          }
          break;
        }
        case 403: {
          toastService.showError('You are not authorized to perform this action.');
          authService.navigateToUnauthorized();
          break;
        }
        case 404: {
          const msg = problem?.detail ?? 'The resource you requested could not be found.';
          toastService.showError(msg);
          break;
        }
        case 409: {
          const msg = errors?.[0]?.message ?? problem?.detail ?? 'A conflict occurred.';
          toastService.showError(msg);
          break;
        }
        default: {
          if (error.status >= 500) {
            toastService.showError('Something went wrong on our end. Please try again later.');
          } else if (error.status === 0) {
            toastService.showError('Unable to reach the server. Check your connection.');
          } else {
            toastService.showError('An unexpected error occurred.');
          }
        }
      }
      return throwError(() => error);
    }),
  );
};