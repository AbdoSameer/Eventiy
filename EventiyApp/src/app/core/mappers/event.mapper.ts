import { Event, EventCardDto, EventDetailsDto } from '../models/event.model';

export function eventCardToEvent(dto: EventCardDto): Event {
  return {
    id: dto.id,
    title: dto.title,
    description: dto.description ?? '',
    type: dto.type,
    date: dto.date,
    location: `${dto.city}, ${dto.country}`,
    price: dto.lowestPrice,
    capacity: dto.totalCapacity,
    attendeeCount: dto.totalSold,
    status: dto.status,
    coverPhotoUrl: dto.coverPhotoUrl,
    city: dto.city,
    country: dto.country,
    currency: dto.currency,
    latitude: dto.latitude,
    longitude: dto.longitude,
  };
}

export function eventDetailsToEvent(dto: EventDetailsDto): Event {
  return {
    id: dto.id,
    title: dto.name,
    description: dto.description,
    type: dto.type,
    date: dto.date,
    location: `${dto.location.city}, ${dto.location.country}`,
    price: dto.lowestTicketPrice,
    capacity: dto.ticketDetails.reduce((sum, t) => sum + t.capacity, 0),
    attendeeCount: dto.totalSold,
    status: dto.status,
    coverPhotoUrl: dto.coverPhotoUrl,
    photos: dto.photos,
    ticketTypes: dto.ticketDetails,
    city: dto.location.city,
    country: dto.location.country,
    currency: dto.ticketDetails[0]?.currency ?? 'USD',
    latitude: dto.location.latitude,
    longitude: dto.location.longitude,
  };
}
