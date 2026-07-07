import { BookingStatus } from '../enums/booking-status.enum';

export type { BookingStatus };

export interface CreateBookingRequest {
  eventId: string;
  ticketTypeId: string;
  quantity: number;
}

export interface BackendBookingDetails {
  id: string;
  eventId: string;
  userId: string;
  ticketTypeId: string;
  eventTitle: string;
  quantity: number;
  bookingDate: string;
  status: string;
  totalAmount: number;
  currency: string;
}

export interface BookingByEventResponse {
  id: string;
  eventId: string;
  userId: string;
  bookingDate: string;
  quantity: number;
  totalAmount: number;
}

export interface BackendBookingByUser {
  id: string;
  eventId: string;
  eventTitle: string;
  eventDate: string;
  eventCity: string;
  ticketTypeName: string;
  quantity: number;
  totalAmount: number;
  currency: string;
  status: string;
  bookingDate: string;
}

export interface Booking {
  id: string;
  eventId: string;
  eventTitle: string;
  eventCoverImageUrl?: string;
  eventDate?: string;
  eventLocation?: string;
  ticketTypeName: string;
  quantity: number;
  totalAmount: number;
  status: BookingStatus;
  createdAt: string;
  attendeeName?: string;
}
