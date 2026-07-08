import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

/**
 * Application routes.
 *
 * Every feature component is lazy-loaded via loadComponent so the initial
 * bundle stays small. Protected routes compose authGuard + roleGuard so
 * authorization is enforced before the component is ever constructed.
 *
 * Route order matters: concrete paths (`events/create`) must appear before
 * the parameterized segment (`events/:id`), and `**` must be last.
 */
export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/home/home.component').then((m) => m.HomeComponent),
    title: 'Eventiy — Find Your Next Event',
  },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then((m) => m.LoginComponent),
    title: 'Log in — Eventiy',
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register/register.component').then((m) => m.RegisterComponent),
    title: 'Sign up — Eventiy',
  },
  {
    path: 'events',
    loadComponent: () => import('./features/events/event-list/event-list.component').then((m) => m.EventListComponent),
    title: 'Browse Events — Eventiy',
  },
  // Static path BEFORE the parameterized one so `/events/create` wins over `/events/:id`.
  {
    path: 'events/create',
    canActivate: [authGuard, roleGuard(['Organizer', 'Admin'])],
    loadComponent: () => import('./features/events/event-create/event-create.component').then((m) => m.EventCreateComponent),
    title: 'Create Event — Eventiy',
  },
  {
    path: 'events/:id',
    loadComponent: () => import('./features/events/event-detail/event-detail.component').then((m) => m.EventDetailComponent),
    title: 'Event Details — Eventiy',
  },
  {
    path: 'events/:id/edit',
    canActivate: [authGuard, roleGuard(['Admin', 'Organizer'])],
    loadComponent: () => import('./features/events/event-edit/event-edit.component').then((m) => m.EventEditComponent),
    title: 'Edit Event — Eventiy',
  },
  {
    path: 'dashboard/organizer',
    canActivate: [authGuard, roleGuard(['Organizer', 'Admin'])],
    loadComponent: () => import('./features/dashboard/organizer-dashboard/organizer-dashboard.component').then((m) => m.OrganizerDashboardComponent),
    title: 'Organizer Dashboard — Eventiy',
  },
  {
    path: 'dashboard/attendee',
    canActivate: [authGuard, roleGuard(['Attendee'])],
    loadComponent: () => import('./features/dashboard/attendee-dashboard/attendee-dashboard.component').then((m) => m.AttendeeDashboardComponent),
    title: 'My Bookings — Eventiy',
  },
  {
    path: 'unauthorized',
    loadComponent: () => import('./presentation/features/errors/unauthorized/unauthorized.component').then((m) => m.UnauthorizedComponent),
    title: 'Unauthorized — Eventiy',
  },
  {
    path: '**',
    loadComponent: () => import('./presentation/features/errors/not-found/not-found.component').then((m) => m.NotFoundComponent),
    title: 'Page Not Found — Eventiy',
  },
];
