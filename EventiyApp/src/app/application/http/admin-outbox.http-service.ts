import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { HttpClientBase } from './http-client-base';
import { Result } from '../../core/models/result.model';

export interface DeadLetterDto {
  id: string;
  eventName: string;
  domain: string;
  payload: string;
  occurredOnUtc: string;
  idempotencyKey: string;
  retryCount: number;
  failedReason: string;
  movedToDeadLetterAt: string;
}

@Injectable({ providedIn: 'root' })
export class AdminOutboxHttpService extends HttpClientBase {
  private readonly baseUrl = `${this.apiUrl}/admin/outbox`;

  getDeadLetters(): Observable<Result<DeadLetterDto[]>> {
    return this.http.get<DeadLetterDto[]>(`${this.baseUrl}/dead-letters`).pipe(
      map((data): Result<DeadLetterDto[]> => ({ isSuccess: true, isFailure: false, value: data })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  requeueDeadLetter(id: string): Observable<Result<boolean>> {
    return this.http.post<void>(`${this.baseUrl}/dead-letters/${id}/requeue`, {}).pipe(
      map((): Result<boolean> => ({ isSuccess: true, isFailure: false, value: true })),
      catchError((err) => this.toErrorResult(err)),
    );
  }
}
