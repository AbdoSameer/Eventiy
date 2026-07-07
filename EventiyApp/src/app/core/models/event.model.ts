import { EventStatus } from '../enums/event-status.enum';

export type { EventStatus };

export interface AddressDto {
  country: string;
  city: string;
  street: string;
  latitude?: number | null;
  longitude?: number | null;
}

export interface TicketDetailsDto {
  id: string;
  price: number;
  currency: string;
  name: string;
  capacity: number;
}

export interface EventPhotoResponse {
  id: string;
  publicUrl: string;
  caption: string | null;
  displayOrder: number;
  isCover: boolean;
  uploadedAt: string;
}

export interface PaginatedEventResponse {
  items: EventCardDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface EventCardDto {
  id: string;
  title: string;
  date: string;
  city: string;
  country: string;
  lowestPrice: number;
  currency: string;
  status: EventStatus;
  totalSold: number;
  totalCapacity: number;
  ticketTypeCount: number;
  description?: string;
  coverPhotoUrl?: string;
  type: string;
  latitude?: number | null;
  longitude?: number | null;
}

export interface EventDetailsDto {
  id: string;
  name: string;
  date: string;
  description: string;
  status: EventStatus;
  type: string;
  lowestTicketPrice: number;
  totalSold: number;
  location: AddressDto;
  ticketDetails: TicketDetailsDto[];
  coverPhotoUrl?: string;
  photos?: EventPhotoResponse[];
}

export interface CreateEventCommand {
  name: string;
  capacity: number;
  date: string;
  location: AddressDto;
  description: string;
  type: string;
  latitude?: number | null;
  longitude?: number | null;
}

export interface UpdateEventCommand {
  name: string;
  capacity: number;
  date: string;
  location: AddressDto;
  description: string;
  type: string;
  latitude?: number | null;
  longitude?: number | null;
}

export interface AddTicketTypeRequest {
  name: string;
  amount: number;
  currency: string;
  capacity: number;
}

export interface Event {
  id: string;
  title: string;
  description: string;
  category: string;
  date: string;
  location: string;
  price: number;
  capacity: number;
  attendeeCount: number;
  coverImageUrl?: string;
  coverPhotoUrl?: string;
  photos?: EventPhotoResponse[];
  organizerName?: string;
  ticketTypes?: TicketDetailsDto[];
  status?: EventStatus;
  city?: string;
  country?: string;
  currency?: string;
  type?: string;
  latitude?: number | null;
  longitude?: number | null;
}

export const EVENT_CATEGORIES = [
  'Music',
  'Tech',
  'Sports',
  'Art',
  'Food',
  'Education',
  'Theater',
  'Outdoors',
] as const;

export type EventCategory = (typeof EVENT_CATEGORIES)[number];

export const CATEGORY_META: Record<string, { emoji: string; label: string }> = {
  Music: { emoji: '🎵', label: 'Music' },
  Tech: { emoji: '💻', label: 'Tech' },
  Sports: { emoji: '🏃', label: 'Sports' },
  Art: { emoji: '🎨', label: 'Art' },
  Food: { emoji: '🍔', label: 'Food' },
  Education: { emoji: '📚', label: 'Education' },
  Theater: { emoji: '🎭', label: 'Theater' },
  Outdoors: { emoji: '🌿', label: 'Outdoors' },
};
