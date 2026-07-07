import { Booking, BackendBookingByUser, BookingStatus } from '../models/booking.model';

export function bookingByUserToBooking(dto: BackendBookingByUser): Booking {
  return {
    id: dto.id,
    eventId: dto.eventId,
    eventTitle: dto.eventTitle,
    eventDate: dto.eventDate,
    eventLocation: dto.eventCity,
    ticketTypeName: dto.ticketTypeName,
    quantity: dto.quantity,
    totalAmount: dto.totalAmount,
    status: dto.status as BookingStatus,
    createdAt: dto.bookingDate,
  };
}
