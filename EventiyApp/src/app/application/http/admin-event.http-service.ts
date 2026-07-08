import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { HttpClientBase } from './http-client-base';
import { AddTicketTypeRequest } from '../../core/models/event.model';
import { Result } from '../../core/models/result.model';

export interface AdminUpdateEventRequest {
  name: string;
  capacity: number;
  date: string;
  location: { country: string; city: string; street: string };
  description: string;
}

@Injectable({ providedIn: 'root' })
export class AdminEventHttpService extends HttpClientBase {
  private readonly baseUrl = `${this.apiUrl}/admin/events`;

  updateEvent(id: string, data: AdminUpdateEventRequest): Observable<Result<boolean>> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, data).pipe(
      map((): Result<boolean> => ({ isSuccess: true, isFailure: false, value: true })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  addTicketType(eventId: string, data: AddTicketTypeRequest): Observable<Result<boolean>> {
    return this.http.post<void>(`${this.baseUrl}/${eventId}/ticket-types`, data).pipe(
      map((): Result<boolean> => ({ isSuccess: true, isFailure: false, value: true })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  publishEvent(eventId: string): Observable<Result<boolean>> {
    return this.http.post<void>(`${this.baseUrl}/${eventId}/publish`, {}).pipe(
      map((): Result<boolean> => ({ isSuccess: true, isFailure: false, value: true })),
      catchError((err) => this.toErrorResult(err)),
    );
  }
}
