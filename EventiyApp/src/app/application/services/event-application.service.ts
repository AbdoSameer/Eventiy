import { Injectable, signal } from '@angular/core';
import { Observable, forkJoin, of } from 'rxjs';
import { map, catchError, shareReplay } from 'rxjs/operators';
import { EventHttpService, EventQuery } from '../http/event.http-service';
import { EventPhotoHttpService } from '../http/event-photo.http-service';
import {
  Event,
  EventPhotoResponse,
} from '../../core/models/event.model';
import { eventCardToEvent, eventDetailsToEvent } from '../../core/mappers/event.mapper';
import { Result } from '../../core/models/result.model';

function cacheKey(query?: EventQuery): string {
  const q = query ?? {};
  return `${q.page ?? 1}:${q.pageSize ?? 10}:${q.type ?? ''}:${q.keyword ?? ''}:${q.userLatitude ?? ''}:${q.userLongitude ?? ''}:${q.distanceInKm ?? ''}`;
}

@Injectable({ providedIn: 'root' })
export class EventApplicationService {
  private readonly cache = new Map<string, { data$: Observable<Result<Event[]>>; timestamp: number }>();
  private readonly CACHE_TTL_MS = 30_000;

  readonly userLocation = signal<{ lat: number; lng: number } | null>(null);
  readonly nearbyEnabled = signal(false);

  constructor(
    private readonly eventHttp: EventHttpService,
    private readonly photoHttp: EventPhotoHttpService,
  ) {}

  requestUserLocation(): void {
    if (!navigator.geolocation) return;
    navigator.geolocation.getCurrentPosition(
      (position) => {
        this.userLocation.set({
          lat: position.coords.latitude,
          lng: position.coords.longitude,
        });
        this.invalidateCache();
      },
      () => {
        this.nearbyEnabled.set(false);
      },
      { enableHighAccuracy: true, timeout: 10000, maximumAge: 300000 },
    );
  }

  toggleNearby(): void {
    const enabled = !this.nearbyEnabled();
    this.nearbyEnabled.set(enabled);
    if (enabled) {
      if (!this.userLocation()) {
        this.requestUserLocation();
      } else {
        this.invalidateCache();
      }
    } else {
      this.invalidateCache();
    }
  }

  getEvents(forceRefresh = false, query?: EventQuery): Observable<Result<Event[]>> {
    const mergedQuery: EventQuery = { ...query };
    if (this.nearbyEnabled()) {
      const loc = this.userLocation();
      if (loc) {
        mergedQuery.userLatitude = loc.lat;
        mergedQuery.userLongitude = loc.lng;
        mergedQuery.distanceInKm = 20;
      }
    }

    const key = cacheKey(mergedQuery);
    const entry = this.cache.get(key);

    if (!forceRefresh && entry && Date.now() - entry.timestamp < this.CACHE_TTL_MS) {
      return entry.data$;
    }

    const data$ = this.eventHttp.getEvents(mergedQuery).pipe(
      map(result => {
        if (!result.isSuccess) return result as unknown as Result<Event[]>;
        return { isSuccess: true, isFailure: false, value: result.value!.map(eventCardToEvent) } as Result<Event[]>;
      }),
      shareReplay({ bufferSize: 1, refCount: true }),
      catchError(err => of({ isSuccess: false, isFailure: true, errors: [{ code: 'cache.error', message: err.message, type: 1 }] } as Result<Event[]>)),
    );

    this.cache.set(key, { data$, timestamp: Date.now() });
    return data$;
  }

  private invalidateCache(): void {
    this.cache.clear();
  }

  getEvent(id: string): Observable<Result<Event>> {
    return this.eventHttp.getEvent(id).pipe(
      map(result => {
        if (!result.isSuccess) return result as unknown as Result<Event>;
        return { isSuccess: true, isFailure: false, value: eventDetailsToEvent(result.value!) } as Result<Event>;
      }),
    );
  }

  getEventWithRelated(id: string): Observable<{ event: Result<Event>; related: Result<Event[]> }> {
    const event$ = this.getEvent(id);
    const related$ = this.getEvents().pipe(
      map(result => {
        if (!result.isSuccess) return result as unknown as Result<Event[]>;
        return { isSuccess: true, isFailure: false, value: result.value!.filter(e => e.id !== id).slice(0, 3) } as Result<Event[]>;
      }),
    );
    return forkJoin({ event: event$, related: related$ });
  }

  createEvent(data: { name: string; capacity: number; date: string; description: string; location: { country: string; city: string; street: string }; type: string }): Observable<Result<string>> {
    this.invalidateCache();
    const loc = this.userLocation();
    return this.eventHttp.createEvent({
      ...data,
      type: data.type,
      latitude: loc?.lat ?? null,
      longitude: loc?.lng ?? null,
    });
  }

  deleteEvent(id: string): Observable<Result<boolean>> {
    this.invalidateCache();
    return this.eventHttp.deleteEvent(id);
  }

  uploadPhotos(eventId: string, files: File[]): Observable<Result<EventPhotoResponse[]>> {
    this.invalidateCache();
    return this.photoHttp.uploadPhotos(eventId, files);
  }

  deletePhoto(eventId: string, photoId: string): Observable<Result<boolean>> {
    this.invalidateCache();
    return this.photoHttp.deletePhoto(eventId, photoId);
  }

  setCoverPhoto(eventId: string, photoId: string): Observable<Result<boolean>> {
    this.invalidateCache();
    return this.photoHttp.setCoverPhoto(eventId, photoId);
  }

  updatePhotoMetadata(eventId: string, photoId: string, data: { caption?: string | null; displayOrder?: number | null }): Observable<Result<boolean>> {
    return this.photoHttp.updatePhotoMetadata(eventId, photoId, data);
  }

  reorderPhotos(eventId: string, orderedPhotoIds: string[]): Observable<Result<boolean>> {
    return this.photoHttp.reorderPhotos(eventId, orderedPhotoIds);
  }

  getPhotos(eventId: string): Observable<Result<EventPhotoResponse[]>> {
    return this.photoHttp.getPhotos(eventId);
  }
}
