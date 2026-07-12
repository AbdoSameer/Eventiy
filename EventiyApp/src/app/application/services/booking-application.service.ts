import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { BookingHttpService } from '../http/booking.http-service';
import { Booking, BackendBookingDetails, BackendBookingByUser, BookingByEventResponse, CreateBookingRequest, CreateBookingResponse } from '../../core/models/booking.model';
import { bookingByUserToBooking } from '../../core/mappers/booking.mapper';
import { Result } from '../../core/models/result.model';

@Injectable({ providedIn: 'root' })
export class BookingApplicationService {
  constructor(private readonly bookingHttp: BookingHttpService) {}

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
}
