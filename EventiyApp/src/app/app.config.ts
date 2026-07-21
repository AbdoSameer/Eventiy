import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { routes } from './app.routes';
import { authInterceptor } from './infrastructure/interceptors/auth.interceptor';
import { errorInterceptor } from './infrastructure/interceptors/error.interceptor';

/**
 * Standalone application configuration.
 *
 * - Functional interceptors attach the JWT and translate the Result pattern
 *   failures / HTTP errors into toast notifications.
 * - Functional guards in app.routes.ts protect role-specific routes.
 * - Component input binding lets routes feed `id` params directly into inputs.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(
      routes,
      withComponentInputBinding(),
      withInMemoryScrolling({
        scrollPositionRestoration: 'top',
        anchorScrolling: 'enabled',
      }),
    ),
    provideAnimations(),
    provideHttpClient(
      withFetch(),
      withInterceptors([errorInterceptor, authInterceptor]),
    ),
  ],
};
