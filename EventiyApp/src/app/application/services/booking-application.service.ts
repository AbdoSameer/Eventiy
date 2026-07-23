import { Injectable, inject } from '@angular/core';
import { Observable, forkJoin, of } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { BookingHttpService } from '../http/booking.http-service';
import { Booking, BackendBookingDetails, BackendBookingByUser, BookingByEventResponse, CreateBookingRequest, CreateBookingResponse } from '../../core/models/booking.model';
import { bookingByUserToBooking } from '../../core/mappers/booking.mapper';
import { Result } from '../../core/models/result.model';
import { Event } from '../../core/models/event.model';

export interface InstantPaymentResult {
  type: 'instant';
  bookingId: string;
  paymentUrl: string | null;
}

export interface MockPaymentResult {
  type: 'mock';
  bookingId: string;
}

export interface DeferredPaymentResult {
  type: 'deferred';
  bookingId: string;
  referenceCode: string;
  holdExpiresAt: string;
}

export type CreateBookingFlowResult = InstantPaymentResult | MockPaymentResult | DeferredPaymentResult;

@Injectable({ providedIn: 'root' })
export class BookingApplicationService {
  private readonly bookingHttp = inject(BookingHttpService);

  createBooking(data: CreateBookingRequest): Observable<Result<CreateBookingResponse>> {
    return this.bookingHttp.createBooking(data);
  }

  getBooking(id: string): Observable<Result<BackendBookingDetails>> {
    return this.bookingHttp.getBooking(id);
  }

  getBookingsByEvent(eventId: string): Observable<Result<BookingByEventResponse[]>> {
    return this.bookingHttp.getBookingsByEvent(eventId);
  }

  getMyBookings(): Observable<Result<Booking[]>> {
    return this.bookingHttp.getMyBookings().pipe(
      map(result => {
        if (!result.isSuccess) return result as unknown as Result<Booking[]>;
        return { isSuccess: true, isFailure: false, value: result.value!.map(bookingByUserToBooking) } as Result<Booking[]>;
      }),
    );
  }

  confirmBooking(id: string): Observable<Result<boolean>> {
    return this.bookingHttp.confirmBooking(id);
  }

  cancelBooking(id: string): Observable<Result<boolean>> {
    return this.bookingHttp.cancelBooking(id);
  }

  confirmDeferredPayment(referenceCode: string): Observable<Result<boolean>> {
    return this.bookingHttp.confirmDeferredPayment({ referenceCode });
  }

  createBookingFlow(data: CreateBookingRequest): Observable<Result<CreateBookingFlowResult>> {
    return this.bookingHttp.createBooking(data).pipe(
      switchMap(result => {
        if (!result.isSuccess || !result.value) {
          return of(result as unknown as Result<CreateBookingFlowResult>);
        }

        const { bookingId, paymentUrl } = result.value;

        if (data.paymentMethod === 'Instant') {
          if (paymentUrl && !paymentUrl.startsWith('mock://')) {
            return of({
              isSuccess: true,
              isFailure: false,
              value: { type: 'instant', bookingId, paymentUrl } as CreateBookingFlowResult,
            });
          }
          return this.bookingHttp.confirmBooking(bookingId).pipe(
            map(confirmResult => ({
              isSuccess: confirmResult.isSuccess,
              isFailure: confirmResult.isFailure,
              value: { type: 'mock', bookingId } as CreateBookingFlowResult,
              errors: confirmResult.isFailure ? confirmResult.errors : undefined,
            }) as Result<CreateBookingFlowResult>),
          );
        }

        return this.bookingHttp.getBooking(bookingId).pipe(
          map(detailResult => {
            if (!detailResult.isSuccess || !detailResult.value) {
              return { isSuccess: false, isFailure: true, errors: detailResult.errors } as Result<CreateBookingFlowResult>;
            }
            const detail = detailResult.value;
            if (!detail.referenceCode || !detail.holdExpiresAt) {
              return { isSuccess: false, isFailure: true, errors: [{ code: 'NoReference', message: 'No reference code for deferred payment.', type: 1 }] } as Result<CreateBookingFlowResult>;
            }
            return {
              isSuccess: true,
              isFailure: false,
              value: { type: 'deferred', bookingId, referenceCode: detail.referenceCode, holdExpiresAt: detail.holdExpiresAt } as CreateBookingFlowResult,
            };
          }),
        );
      }),
    );
  }

  getBookingsForOrganizer(events: Event[]): Observable<Booking[]> {
    if (events.length === 0) return of([]);

    const eventMap = new Map(events.map((e) => [e.id, e]));
    const bookingRequests = events.map((event) =>
      this.bookingHttp.getBookingsByEvent(event.id).pipe(
        map((result) => (result.isSuccess && result.value ? result.value : [])),
        catchError(() => of([])),
      ),
    );

    return forkJoin(bookingRequests).pipe(
      map((results) => {
        return results.flat().map((b: BookingByEventResponse) => {
          const evt = eventMap.get(b.eventId);
          return {
            id: b.id,
            eventId: b.eventId,
            eventTitle: evt?.title ?? 'Unknown Event',
            ticketTypeName: '',
            quantity: b.quantity,
            totalAmount: b.totalAmount,
            status: (b.status as import('../../core/models/booking.model').BookingStatus) ?? 'Pending',
            createdAt: b.bookingDate,
          } as Booking;
        });
      }),
    );
  }
}
