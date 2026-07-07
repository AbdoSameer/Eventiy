import { Pipe, PipeTransform } from '@angular/core';

/**
 * Formats an ISO-8601 date string as: "Mon, Jan 15 • 7:00 PM"
 *
 * Usage:  {{ event.date | dateFormat }}
 */
@Pipe({ name: 'dateFormat', standalone: true })
export class DateFormatPipe implements PipeTransform {
  transform(value: string | Date | null | undefined): string {
    if (!value) {
      return '';
    }
    const date = typeof value === 'string' ? new Date(value) : value;
    if (Number.isNaN(date.getTime())) {
      return '';
    }
    const dayPart = date.toLocaleDateString('en-US', {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
    });
    const timePart = date.toLocaleTimeString('en-US', {
      hour: 'numeric',
      minute: '2-digit',
      hour12: true,
    });
    return `${dayPart} • ${timePart}`;
  }
}
