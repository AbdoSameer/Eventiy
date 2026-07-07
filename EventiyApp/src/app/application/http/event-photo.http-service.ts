import { HttpErrorResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { EventPhotoResponse } from '../../core/models/event.model';
import { Result } from '../../core/models/result.model';
import { HttpClientBase } from './http-client-base';

export interface UpdatePhotoMetadataRequest {
  caption?: string | null;
  displayOrder?: number | null;
}

@Injectable({ providedIn: 'root' })
export class EventPhotoHttpService extends HttpClientBase {
  private readonly baseUrl = `${this.apiUrl}/event`;

  getPhotos(eventId: string): Observable<Result<EventPhotoResponse[]>> {
    return this.http.get<EventPhotoResponse[]>(`${this.baseUrl}/${eventId}/photos`).pipe(
      map((value): Result<EventPhotoResponse[]> => ({ isSuccess: true, isFailure: false, value })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  uploadPhotos(eventId: string, files: File[]): Observable<Result<EventPhotoResponse[]>> {
    const formData = new FormData();
    files.forEach(f => formData.append('photos', f));
    return this.http.post<EventPhotoResponse[]>(`${this.baseUrl}/${eventId}/photos`, formData).pipe(
      map((value): Result<EventPhotoResponse[]> => ({ isSuccess: true, isFailure: false, value })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  deletePhoto(eventId: string, photoId: string): Observable<Result<boolean>> {
    return this.http.delete<void>(`${this.baseUrl}/${eventId}/photos/${photoId}`).pipe(
      map((): Result<boolean> => ({ isSuccess: true, isFailure: false, value: true })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  setCoverPhoto(eventId: string, photoId: string): Observable<Result<boolean>> {
    return this.http.put<void>(`${this.baseUrl}/${eventId}/photos/${photoId}/cover`, {}).pipe(
      map((): Result<boolean> => ({ isSuccess: true, isFailure: false, value: true })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  updatePhotoMetadata(eventId: string, photoId: string, data: UpdatePhotoMetadataRequest): Observable<Result<boolean>> {
    return this.http.put<void>(`${this.baseUrl}/${eventId}/photos/${photoId}/metadata`, data).pipe(
      map((): Result<boolean> => ({ isSuccess: true, isFailure: false, value: true })),
      catchError((err) => this.toErrorResult(err)),
    );
  }

  reorderPhotos(eventId: string, orderedPhotoIds: string[]): Observable<Result<boolean>> {
    return this.http.put<void>(`${this.baseUrl}/${eventId}/photos/reorder`, { orderedPhotoIds }).pipe(
      map((): Result<boolean> => ({ isSuccess: true, isFailure: false, value: true })),
      catchError((err) => this.toErrorResult(err)),
    );
  }
}
