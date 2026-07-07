import { HttpErrorResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import {
  BackendBookingDetails,
  BackendBookingByUser,
  BookingByEventResponse,
  CreateBookingRequest,
} from '../../core/models/booking.model';
import { Result } from '../../core/models/result.model';
import { HttpClientBase } from './http-client-base';

@Injectable({ providedIn: 'root' })
export class BookingHttpService extends HttpClientBase {
  private readonly baseUrl = `${this.apiUrl}/booking`;

  createBooking(data: CreateBookingRequest): Observable<Result<string>> {
    return this.http.post<string>(this.baseUrl, data).pipe(
      map((value): Result<string> => ({ isSuccess: true, isFailure: false, value })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  getBooking(id: string): Observable<Result<BackendBookingDetails>> {
    return this.http.get<BackendBookingDetails>(`${this.baseUrl}/${id}`).pipe(
      map((value): Result<BackendBookingDetails> => ({ isSuccess: true, isFailure: false, value })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  getBookingsByEvent(eventId: string): Observable<Result<BookingByEventResponse[]>> {
    return this.http.get<BookingByEventResponse[]>(`${this.baseUrl}/event/${eventId}`).pipe(
      map((value): Result<BookingByEventResponse[]> => ({ isSuccess: true, isFailure: false, value })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  getMyBookings(): Observable<Result<BackendBookingByUser[]>> {
    return this.http.get<BackendBookingByUser[]>(`${this.baseUrl}/my`).pipe(
      map((value): Result<BackendBookingByUser[]> => ({ isSuccess: true, isFailure: false, value })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  confirmBooking(id: string): Observable<Result<boolean>> {
    return this.http.post<void>(`${this.baseUrl}/${id}/confirm`, {}).pipe(
      map((): Result<boolean> => ({ isSuccess: true, isFailure: false, value: true })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  cancelBooking(id: string): Observable<Result<boolean>> {
    return this.http.put<void>(`${this.baseUrl}/${id}/cancel`, {}).pipe(
      map((): Result<boolean> => ({ isSuccess: true, isFailure: false, value: true })),
      catchError((err) => this.toErrorResult(err)),
    );
  }
}
