import { HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import {
  EventCardDto,
  EventDetailsDto,
  PaginatedEventResponse,
  CreateEventCommand,
  AddTicketTypeRequest,
} from '../../core/models/event.model';
import { Result } from '../../core/models/result.model';
import { HttpClientBase } from './http-client-base';

export interface EventQuery {
  keyword?: string;
  page?: number;
  pageSize?: number;
  type?: string;
  userLatitude?: number | null;
  userLongitude?: number | null;
  distanceInKm?: number;
}

@Injectable({ providedIn: 'root' })
export class EventHttpService extends HttpClientBase {
  private readonly baseUrl = `${this.apiUrl}/event`;

  getEvents(query?: EventQuery): Observable<Result<EventCardDto[]>> {
    let params = new HttpParams();
    if (query?.keyword) params = params.set('keyword', query.keyword);
    if (query?.type) params = params.set('type', query.type);
    if (query?.page) params = params.set('page', query.page);
    if (query?.pageSize) params = params.set('pageSize', query.pageSize);
    if (query?.userLatitude != null) params = params.set('userLatitude', query.userLatitude);
    if (query?.userLongitude != null) params = params.set('userLongitude', query.userLongitude);
    if (query?.distanceInKm != null) params = params.set('distanceInKm', query.distanceInKm);
    return this.http.get<PaginatedEventResponse>(this.baseUrl, { params }).pipe(
      map((res): Result<EventCardDto[]> => ({ isSuccess: true, isFailure: false, value: res.items })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  getEvent(id: string): Observable<Result<EventDetailsDto>> {
    return this.http.get<EventDetailsDto>(`${this.baseUrl}/${id}`).pipe(
      map((value): Result<EventDetailsDto> => ({ isSuccess: true, isFailure: false, value })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  createEvent(data: CreateEventCommand): Observable<Result<string>> {
    return this.http.post<string>(this.baseUrl, data).pipe(
      map((value): Result<string> => ({ isSuccess: true, isFailure: false, value })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  deleteEvent(id: string): Observable<Result<boolean>> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`).pipe(
      map((): Result<boolean> => ({ isSuccess: true, isFailure: false, value: true })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  updateEvent(id: string, data: { name: string; capacity: number; date: string; location: { country: string; city: string; street: string }; description: string }): Observable<Result<boolean>> {
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
}
