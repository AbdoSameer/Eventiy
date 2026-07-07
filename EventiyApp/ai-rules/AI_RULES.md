Eventiy AI Development Rules
Reference this file in your AI prompts when extending the Eventiy Angular frontend.
Quick Start
When asking AI to extend Eventiy, include this in your prompt:
Reference the AI rules file: docs/AI_RULES.md

Follow the exact patterns and conventions documented in this file.
Reference AuthService, EventService, and EventCardComponent for real implementation examples.
What Eventiy Provides
Eventiy is a hardened Angular foundation that solves the hard problems so AI can focus on features:
✅ Already Solved (Don’t Let AI Recreate)
•	Authentication: JWT token management with automatic refresh
•	Authorization: Role-based access control (Attendee, Organizer, Admin)
•	Error Handling: Centralized Result-pattern error interceptor
•	State Management: Angular Signals for reactive user state
•	HTTP Layer: Services extending EndpointBase with auth headers
•	Routing: Lazy-loaded feature modules with AuthGuard and RoleGuard
•	UI Foundation: Tailwind CSS, Inter font, Coral Red (#F6544C) theme
•	Backend Integration: Mapped to CQRS/Result-pattern backend
🎯 What AI Should Build
•	New Features: Following existing component and service patterns
•	New Pages: Following lazy-loaded feature structure
•	UI Components: Following standalone component architecture
•	Business Logic: In services, not components
________________________________________
Frontend Patterns (Angular 17+)
Project Structure
src/app/
├── core/
│   ├── interceptors/     # Auth & Error interceptors
│   ├── guards/            # AuthGuard, RoleGuard
│   ├── services/          # API services (Auth, Event, Booking)
│   └── models/            # TypeScript interfaces
├── features/
│   ├── home/              # Hero, Categories, EventsGrid
│   ├── auth/              # Login, Register
│   ├── events/            # List, Detail, Create
│   └── dashboard/         # Organizer, Attendee dashboards
├── shared/
│   ├── components/        # Navbar, EventCard, SearchBar, Toast
│   └── pipes/             # DateFormatPipe
└── app.routes.ts          # Lazy-loaded routes
Models
Location: src/app/core/models/
Model Rules: 1. ✅ Use TypeScript interfaces (not classes for data models) 2. ✅ Use ? for optional properties 3. ✅ Match backend ViewModel structure from CQRS handlers 4. ✅ Use camelCase for property names (TypeScript convention) 5. ✅ Flatten navigation properties (e.g., categoryName instead of category.name) 6. ❌ DO NOT use classes for simple data models
Reference Models:
// auth.model.ts
export interface AuthResponse {
  userId: string;
  email: string;
  role: 'Attendee' | 'Organizer' | 'Admin';
  token: string;
  expiresAt: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  role: 'Attendee' | 'Organizer' | 'Admin';
}

// event.model.ts
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
  organizerName: string;
}

export interface CreateEventRequest {
  title: string;
  description: string;
  category: string;
  date: string;
  location: string;
  ticketTypes: TicketTypeRequest[];
}

export interface TicketTypeRequest {
  name: string;
  price: number;
  quantity: number;
}

// booking.model.ts
export interface Booking {
  id: string;
  eventId: string;
  eventTitle: string;
  ticketTypeName: string;
  quantity: number;
  totalAmount: number;
  status: BookingStatus;
  createdAt: string;
}

export type BookingStatus = 'Pending' | 'Confirmed' | 'Cancelled' | 'Completed';
Services
Location: src/app/core/services/
Service Rules: 1. ✅ Use @Injectable({ providedIn: 'root' }) 2. ✅ Use inject() function for dependencies (not constructor injection) 3. ✅ Use Angular Signals for reactive state (currentUser = signal<AuthResponse | null>(null)) 4. ✅ Use HttpClient directly (not extending EndpointBase in this architecture) 5. ✅ Return Observable<T> with generic type 6. ✅ Use environment.apiUrl as base URL 7. ✅ Handle Result pattern from backend ({ value: T } or { errors: [...] }) 8. ❌ DO NOT use constructor injection - use inject() function 9. ❌ DO NOT store UI state in services (use component signals)
Reference Service Pattern:
@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  // Signals for reactive state
  currentUser = signal<AuthResponse | null>(null);
  isAuthenticated = computed(() => !!this.currentUser());
  userRole = computed(() => this.currentUser()?.role ?? null);

  login(credentials: LoginRequest): Observable<Result<AuthResponse>> {
    return this.http.post<Result<AuthResponse>>(`${environment.apiUrl}/auth/login`, credentials)
      .pipe(tap(result => {
        if (result.isSuccess) {
          this.currentUser.set(result.value);
          localStorage.setItem('token', result.value.token);
        }
      }));
  }
}
Components
Location: src/app/features/ or src/app/shared/components/
Component Rules: 1. ✅ Use standalone components - no NgModules 2. ✅ Use inject() function for dependency injection 3. ✅ Implement OnInit for initialization logic 4. ✅ Use Angular Signals for component state (items = signal<Event[]>([])) 5. ✅ Use computed() for derived state (filteredItems = computed(() => ...)) 6. ✅ Use effect() for side effects (logging, localStorage) 7. ✅ Use Tailwind CSS for styling 8. ✅ Use Inter font (already imported in index.html) 9. ✅ Use Coral Red (#F6544C) as primary accent color 10. ✅ Handle errors in subscribe error callback 11. ❌ DO NOT use constructor injection - use inject() 12. ❌ DO NOT use NgModules - standalone only 13. ❌ DO NOT make HTTP calls directly - use services
Reference Component Pattern:
@Component({
  selector: 'app-event-list',
  standalone: true,
  imports: [CommonModule, EventCardComponent],
  templateUrl: './event-list.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EventListComponent implements OnInit {
  private eventService = inject(EventService);

  events = signal<Event[]>([]);
  loading = signal(false);
  error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadEvents();
  }

  loadEvents(): void {
    this.loading.set(true);
    this.eventService.getEvents().subscribe({
      next: (result) => {
        if (result.isSuccess) {
          this.events.set(result.value);
        } else {
          this.error.set(result.errors[0].message);
        }
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set('Failed to load events');
        this.loading.set(false);
      }
    });
  }
}
Interceptors
Location: src/app/core/interceptors/
Interceptor Rules: 1. ✅ Use HttpInterceptorFn (functional interceptors, not classes) 2. ✅ Auth Interceptor: Attach JWT to every request except /auth/* 3. ✅ Error Interceptor: Handle backend Result Failure shape 4. ✅ Return HttpHandlerFn for chain continuation 5. ❌ DO NOT use class-based interceptors - use functional interceptors
Reference Interceptors:
// auth.interceptor.ts
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.currentUser()?.token;

  if (token && !req.url.includes('/auth/')) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    });
  }
  return next(req);
};

// error.interceptor.ts
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toastService = inject(ToastService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 400 && error.error?.errors) {
        // Backend Result Failure pattern
        error.error.errors.forEach((err: any) => {
          toastService.showError(err.message);
        });
      } else {
        toastService.showError('An unexpected error occurred');
      }
      return throwError(() => error);
    })
  );
};
Guards
Location: src/app/core/guards/
Guard Rules: 1. ✅ Use CanActivateFn (functional guards, not classes) 2. ✅ AuthGuard: Redirect to /login if not authenticated 3. ✅ RoleGuard: Redirect to /unauthorized if role doesn’t match 4. ✅ Check AuthService.isAuthenticated() signal 5. ❌ DO NOT use class-based guards - use functional guards
Reference Guards:
// auth.guard.ts
export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }
  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

// role.guard.ts
export const roleGuard = (allowedRoles: string[]): CanActivateFn => {
  return (route, state) => {
    const authService = inject(AuthService);
    const router = inject(Router);
    const userRole = authService.userRole();

    if (userRole && allowedRoles.includes(userRole)) {
      return true;
    }
    return router.createUrlTree(['/unauthorized']);
  };
};
Routing
Location: src/app/app.routes.ts
Routing Rules: 1. ✅ Use lazy loading with loadComponent 2. ✅ Use authGuard for protected routes 3. ✅ Use roleGuard for role-specific routes 4. ✅ Set title for each route 5. ✅ Use path: '**' for 404 route (must be last) 6. ❌ DO NOT use eager loading - always lazy load feature components
Reference Routes:
export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/home/home.component').then(m => m.HomeComponent),
    title: 'Eventiy - Find Your Next Event'
  },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent),
    title: 'Login - Eventiy'
  },
  {
    path: 'events/create',
    loadComponent: () => import('./features/events/event-create/event-create.component').then(m => m.EventCreateComponent),
    canActivate: [authGuard, roleGuard(['Organizer', 'Admin'])],
    title: 'Create Event - Eventiy'
  },
  {
    path: '**',
    loadComponent: () => import('./shared/components/not-found/not-found.component').then(m => m.NotFoundComponent),
    title: 'Page Not Found - Eventiy'
  }
];
Styling (Tailwind CSS)
Rules: 1. ✅ Use Tailwind utility classes exclusively (no custom CSS) 2. ✅ Primary color: #F6544C (Coral Red) 3. ✅ Background: #FFFFFF with #F7F7F7 sections 4. ✅ Font: Inter (imported in index.html) 5. ✅ Cards: rounded-2xl shadow-md hover:shadow-xl transition 6. ✅ Buttons: rounded-full with filled and outline variants 7. ✅ Forms: Inline error messages on blur 8. ✅ Loading: Skeleton loaders (not spinners) 9. ✅ Mobile-first: Use md: and lg: breakpoints 10. ❌ DO NOT write custom CSS - use Tailwind utilities 11. ❌ DO NOT use inline styles - use Tailwind classes
Tailwind Config Extensions:
// tailwind.config.js
module.exports = {
  theme: {
    extend: {
      colors: {
        primary: '#F6544C',
        'primary-dark': '#E53E3E',
        'primary-light': '#FF7B73',
        background: '#F7F7F7',
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
      },
    },
  },
};
Error Handling & Result Pattern
Backend Result Pattern Integration:
// Result pattern from backend
export interface Result<T> {
  isSuccess: boolean;
  isFailure: boolean;
  value?: T;
  errors?: ErrorDetail[];
}

export interface ErrorDetail {
  code: string;
  message: string;
}
Handling in Components:
this.service.getData().subscribe({
  next: (result) => {
    if (result.isSuccess) {
      this.data.set(result.value);
    } else {
      // Handle business logic errors
      this.error.set(result.errors?.[0]?.message);
    }
  },
  error: (err) => {
    // Handle HTTP/network errors
    this.error.set('Network error occurred');
  }
});
________________________________________
Reference Implementations
When implementing new features, reference these real implementations:
Services
•	AuthService (core/services/auth.service.ts) - Signal-based auth state, login/register/logout
•	EventService (core/services/event.service.ts) - CRUD operations, search
•	BookingService (core/services/booking.service.ts) - Create/confirm/cancel bookings
Components
•	NavbarComponent (shared/components/navbar/) - Responsive nav with auth state
•	EventCardComponent (shared/components/event-card/) - Reusable card with Tailwind
•	HomeComponent (features/home/) - Hero, categories, events grid
•	LoginComponent (features/auth/login/) - Reactive form with validation
Guards & Interceptors
•	authGuard (core/guards/auth.guard.ts) - Functional guard pattern
•	authInterceptor (core/interceptors/auth.interceptor.ts) - JWT attachment
•	errorInterceptor (core/interceptors/error.interceptor.ts) - Result pattern handling
________________________________________
Naming Conventions
Files
•	Components: {feature}.component.ts (e.g., event-list.component.ts)
•	Services: {feature}.service.ts (e.g., event.service.ts)
•	Models: {domain}.model.ts (e.g., event.model.ts)
•	Guards: {name}.guard.ts (e.g., auth.guard.ts)
•	Interceptors: {name}.interceptor.ts (e.g., auth.interceptor.ts)
•	Pipes: {name}.pipe.ts (e.g., date-format.pipe.ts)
Classes/Interfaces
•	Components: PascalCase with Component suffix (e.g., EventListComponent)
•	Services: PascalCase with Service suffix (e.g., EventService)
•	Models: PascalCase interface (e.g., Event, Booking)
•	Signals: camelCase (e.g., currentUser, events)
•	Computed: camelCase with descriptive name (e.g., filteredEvents)
________________________________________
Quick Checklist
When creating a new feature:
Model: - [ ] Create interface in core/models/{domain}.model.ts - [ ] Match backend ViewModel structure - [ ] Use camelCase properties
Service: - [ ] Create service in core/services/{feature}.service.ts - [ ] Use inject() for dependencies - [ ] Use environment.apiUrl for base URL - [ ] Return Observable<Result<T>> - [ ] Handle Result pattern from backend
Component: - [ ] Create standalone component - [ ] Use inject() for dependencies - [ ] Use Signals for state (signal(), computed(), effect()) - [ ] Use Tailwind CSS for styling - [ ] Use Inter font - [ ] Use Coral Red (#F6544C) for primary actions
Routing: - [ ] Add lazy-loaded route in app.routes.ts - [ ] Add title property - [ ] Add canActivate guards if protected - [ ] Add to navbar if needed
Guard (if needed): - [ ] Use functional CanActivateFn - [ ] Check AuthService signals - [ ] Return UrlTree for redirects
________________________________________
Critical Rules
❌ Never Do This
•	Don’t create new authentication mechanisms (use AuthService)
•	Don’t use constructor injection (use inject())
•	Don’t use NgModules (use standalone components)
•	Don’t use class-based guards/interceptors (use functional)
•	Don’t write custom CSS (use Tailwind utilities)
•	Don’t skip error handling (always handle Result pattern)
•	Don’t use any type (define proper interfaces)
•	Don’t make HTTP calls from components (use services)
•	Don’t mutate signals directly (use .set() or .update())
✅ Always Do This
•	Use standalone components
•	Use inject() for DI
•	Use Angular Signals for state
•	Use Tailwind CSS for styling
•	Use Inter font and Coral Red color
•	Use lazy loading for routes
•	Use functional guards and interceptors
•	Handle backend Result pattern
•	Add title to routes
•	Follow naming conventions exactly
________________________________________
Backend Integration Rules
API Endpoints
POST   /api/auth/register     → { firstName, lastName, email, password, role }
POST   /api/auth/login        → { email, password }
GET    /api/events            → paginated list
GET    /api/events/{id}       → event details
POST   /api/events            → create (Organizer/Admin only)
PUT    /api/events/{id}       → update
DELETE /api/events/{id}       → cancel
POST   /api/bookings          → create booking (Attendee)
GET    /api/bookings/{id}     → booking details
PUT    /api/bookings/{id}/confirm → confirm (Organizer/Admin)
PUT    /api/bookings/{id}/cancel  → cancel
Result Pattern Response
// Success: { isSuccess: true, isFailure: false, value: T }
// Failure: { isSuccess: false, isFailure: true, errors: [{ code, message }] }
Roles
•	Attendee: Can browse events, create bookings, view own bookings
•	Organizer: Can create/manage events, confirm/cancel bookings
•	Admin: Full access to all features
________________________________________
Remember: Eventiy provides the foundation. AI fills in features following established patterns. Reference real implementations (AuthService, EventService, EventCardComponent) for concrete examples.
