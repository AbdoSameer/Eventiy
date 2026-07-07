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

@Injectable({ providedIn: 'root' })
export class EventApplicationService {
  private eventsCache$: Observable<Result<Event[]>> | null = null;
  private lastFetchTime = 0;
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
    const isExpired = Date.now() - this.lastFetchTime > this.CACHE_TTL_MS;
    if (!forceRefresh && this.eventsCache$ && !isExpired) {
      return this.eventsCache$;
    }

    const mergedQuery: EventQuery = { ...query };
    if (this.nearbyEnabled()) {
      const loc = this.userLocation();
      if (loc) {
        mergedQuery.userLatitude = loc.lat;
        mergedQuery.userLongitude = loc.lng;
        mergedQuery.distanceInKm = 20;
      }
    }

    this.eventsCache$ = this.eventHttp.getEvents(mergedQuery).pipe(
      map(result => {
        if (!result.isSuccess) return result as unknown as Result<Event[]>;
        return { isSuccess: true, isFailure: false, value: result.value!.map(eventCardToEvent) } as Result<Event[]>;
      }),
      shareReplay(1),
      catchError(err => {
        this.eventsCache$ = null;
        return of({ isSuccess: false, isFailure: true, errors: [{ code: 'cache.error', message: err.message, type: 1 }] } as Result<Event[]>);
      }),
    );
    this.lastFetchTime = Date.now();
    return this.eventsCache$;
  }

  private invalidateCache(): void {
    this.eventsCache$ = null;
    this.lastFetchTime = 0;
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

  updateEvent(id: string, data: { name: string; capacity: number; date: string; description: string; location: { country: string; city: string; street: string }; type: string }): Observable<Result<boolean>> {
    this.invalidateCache();
    return this.eventHttp.updateEvent(id, {
      ...data,
      type: data.type,
      latitude: null,
      longitude: null,
    });
  }

  deleteEvent(id: string): Observable<Result<boolean>> {
    this.invalidateCache();
    return this.eventHttp.deleteEvent(id);
  }

  addTicketType(eventId: string, data: { name: string; amount: number; currency: string; capacity: number }): Observable<Result<boolean>> {
    this.invalidateCache();
    return this.eventHttp.addTicketType(eventId, data);
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
