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
    const problem = err.error as Record<string, unknown> | null;
    const rawErrors = (problem?.['errors'] ?? problem?.['error']) as Array<Record<string, unknown>> | undefined;
    const errors = rawErrors?.map(e => ({
      code: String(e['code'] ?? 'unknown'),
      message: String(e['message'] ?? 'An unexpected error occurred'),
      type: Number(e['type'] ?? 1),
    })) ?? [{ code: 'unknown', message: 'An unexpected error occurred', type: 1 }];

    return of({ isSuccess: false, isFailure: true, errors } as Result<never>);
  }
}
