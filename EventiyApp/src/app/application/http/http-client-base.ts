import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Result } from '../../core/models/result.model';

@Injectable({ providedIn: 'root' })
export abstract class HttpClientBase {
  protected readonly http = inject(HttpClient);
  protected readonly apiUrl = environment.apiUrl;

  protected toErrorResult(err: HttpErrorResponse): Observable<Result<never>> {
    if (!environment.production) {
      console.error('[HttpClientBase] Request failed:', err.status, err.url, err.message, err.error);
    }

    const problem = err.error as Record<string, unknown> | null;
    const rawErrors = (problem?.['errors'] ?? problem?.['error']) as Array<Record<string, unknown>> | undefined;
    const errors = rawErrors?.map(e => ({
      code: String(e['code'] ?? 'unknown'),
      message: String(e['message'] ?? 'An unexpected error occurred'),
      type: Number(e['type'] ?? 1),
    })) ?? extractProblemDetails(problem) ?? [{ code: 'unknown', message: extractMessage(err), type: 1 }];

    return of({ isSuccess: false, isFailure: true, errors } as Result<never>);
  }
}

function extractProblemDetails(problem: Record<string, unknown> | null): { code: string; message: string; type: number }[] | undefined {
  if (!problem) return undefined;

  if (problem['errors'] && typeof problem['errors'] === 'object') {
    const validationErrors = problem['errors'] as Record<string, string[]>;
    const messages = Object.values(validationErrors).flat();
    if (messages.length > 0) {
      return messages.map(message => ({
        code: 'Validation',
        message: String(message),
        type: 0,
      }));
    }
  }

  const title = problem['title'] as string | undefined;
  const detail = problem['detail'] as string | undefined;
  if (title || detail) {
    return [{ code: String(title ?? 'Error'), message: String(detail ?? title ?? 'An error occurred'), type: 1 }];
  }

  return undefined;
}

function extractMessage(err: HttpErrorResponse): string {
  if (err.status === 0) return 'Could not reach the server.';
  if (err.status === 401) return 'Authentication required.';
  if (err.status === 403) return 'You are not authorized to perform this action.';
  if (err.status === 404) return 'The requested resource was not found.';
  if (err.status >= 500) return 'A server error occurred. Please try again later.';
  return 'An unexpected error occurred.';
}
