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
  capacity: number;
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
  type: string;
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

export interface CategoryMeta {
  iconPath: string;
  label: string;
}

export const CATEGORY_META: Record<string, CategoryMeta> = {
  Music: { iconPath: 'M9 18V5l12-2v13M9 18a3 3 0 01-6 0 3 3 0 016 0zm12-2a3 3 0 01-6 0 3 3 0 016 0z', label: 'Music' },
  Tech: { iconPath: 'M9 17.25v1.007a3 3 0 01-.879 2.122L7.5 21h9l-.621-.621A3 3 0 0115 18.257V17.25m6-12V15a2.25 2.25 0 01-2.25 2.25H5.25A2.25 2.25 0 013 15V5.25m18 0A2.25 2.25 0 0018.75 3H5.25A2.25 2.25 0 003 5.25m18 0V12a2.25 2.25 0 01-2.25 2.25H5.25A2.25 2.25 0 013 12V5.25', label: 'Tech' },
  Sports: { iconPath: 'M15.75 6a3.75 3.75 0 11-7.5 0 3.75 3.75 0 017.5 0zM4.501 20.118a7.5 7.5 0 0114.998 0A17.933 17.933 0 0112 21.75c-2.676 0-5.216-.584-7.499-1.632z', label: 'Sports' },
  Art: { iconPath: 'M9.53 16.122a3 3 0 00-5.78 1.128 2.25 2.25 0 01-2.4 2.245 4.5 4.5 0 008.4-2.245c0-.399-.078-.78-.22-1.128zm0 0a15.998 15.998 0 003.388-1.62m-5.043-.025a15.994 15.994 0 011.622-3.395m3.42 3.42a15.995 15.995 0 004.764-4.648l3.876-5.814a1.151 1.151 0 00-1.597-1.597L14.146 6.32a15.996 15.996 0 00-4.649 4.763m3.42 3.42a6.776 6.776 0 00-3.42-3.42', label: 'Art' },
  Food: { iconPath: 'M18 18.72a9.094 9.094 0 003.741-.479 3 3 0 00-4.682-2.72m.94 3.198l.001.031c0 .225-.012.447-.037.666A11.944 11.944 0 0112 21c-2.17 0-4.207-.576-5.963-1.584A6.062 6.062 0 016 18.719m12 0a5.971 5.971 0 00-.941-3.197m0 0A5.995 5.995 0 0012 12.75a5.995 5.995 0 00-5.058 2.772m0 0a3 3 0 00-4.681 2.72 8.986 8.986 0 003.74.477m.94-3.197a5.971 5.971 0 00-.94 3.197M15 6.75a3 3 0 11-6 0 3 3 0 016 0zm6 3a2.25 2.25 0 11-4.5 0 2.25 2.25 0 014.5 0zm-13.5 0a2.25 2.25 0 11-4.5 0 2.25 2.25 0 014.5 0z', label: 'Food' },
  Education: { iconPath: 'M4.26 10.147a60.438 60.438 0 00-.491 6.347A48.62 48.62 0 0112 20.904a48.62 48.62 0 018.232-4.41 60.46 60.46 0 00-.491-6.347m-15.482 0a50.636 50.636 0 00-2.658-.813A59.906 59.906 0 0112 3.493a59.903 59.903 0 0110.399 5.84c-.896.248-1.783.52-2.658.814m-15.482 0A50.717 50.717 0 0112 13.489a50.702 50.702 0 017.74-3.342', label: 'Education' },
  Theater: { iconPath: 'M6.75 12a.75.75 0 11-1.5 0 .75.75 0 011.5 0zm7.5 0a.75.75 0 11-1.5 0 .75.75 0 011.5 0zm-7.5 3.75a.75.75 0 11-1.5 0 .75.75 0 011.5 0zm7.5 0a.75.75 0 11-1.5 0 .75.75 0 011.5 0z', label: 'Theater' },
  Outdoors: { iconPath: 'M2.25 15.75l5.159-5.159a2.25 2.25 0 013.182 0l5.159 5.159m-1.5-1.5l1.409-1.409a2.25 2.25 0 013.182 0l2.909 2.909M3.75 21h16.5M3.75 3h16.5v13.5a2.25 2.25 0 01-2.25 2.25H6a2.25 2.25 0 01-2.25-2.25V3z', label: 'Outdoors' },
};
