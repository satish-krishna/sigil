# Authentication Patterns (Supabase OAuth)

Complete patterns for OAuth authentication with Supabase.

## Auth Service

```typescript
// ✅ CORRECT: Supabase OAuth authentication
import { Injectable, inject, signal, computed } from '@angular/core';
import { SupabaseClient } from '@supabase/supabase-js';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private supabase = inject(SupabaseClient);

  // Auth state
  currentUser = signal<User | null>(null);
  loading = signal(false);
  error = signal<string | null>(null);

  // Computed
  isAuthenticated = computed(() => !!this.currentUser());

  constructor() {
    this.loadCurrentUser();
  }

  async signInWithOAuth(provider: 'google' | 'facebook' | 'microsoft' | 'apple') {
    this.loading.set(true);
    this.error.set(null);

    try {
      const { data, error } = await this.supabase.auth.signInWithOAuth({
        provider,
        options: {
          redirectTo: window.location.origin,
        },
      });

      if (error) {
        throw new Error(error.message);
      }

      return data;
    } catch (err: any) {
      this.error.set(err.message || 'Authentication failed');
      throw err;
    } finally {
      this.loading.set(false);
    }
  }

  async signOut() {
    this.loading.set(true);
    this.error.set(null);

    try {
      const { error } = await this.supabase.auth.signOut();
      if (error) throw new Error(error.message);

      this.currentUser.set(null);
    } catch (err: any) {
      this.error.set(err.message || 'Sign out failed');
    } finally {
      this.loading.set(false);
    }
  }

  async loadCurrentUser() {
    try {
      const {
        data: { user },
      } = await this.supabase.auth.getUser();
      this.currentUser.set(user || null);
    } catch (err) {
      console.error('Failed to load user:', err);
      this.currentUser.set(null);
    }
  }

  async refreshToken() {
    const { data, error } = await this.supabase.auth.refreshSession();
    if (error) {
      this.currentUser.set(null);
    }
  }
}
```

## Route Guard

```typescript
// ✅ CORRECT: Functional route guard with signals
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  router.navigate(['/login'], {
    queryParams: { returnUrl: state.url },
  });

  return false;
};

// Usage in routing
export const routes: Routes = [
  {
    path: 'events',
    component: EventsComponent,
    canActivate: [authGuard],
  },
];
```

## Login Component

```typescript
// ✅ CORRECT: OAuth login buttons
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, WebAwesomeModule],
  template: `
    <div class="login-container">
      <h1>Sign In</h1>

      @if (authService.error()) {
        <sl-alert type="error">
          <i class="fa-solid fa-circle-exclamation"></i>
          {{ authService.error() }}
        </sl-alert>
      }

      <div class="oauth-buttons">
        <sl-button (click)="signInWithGoogle()" [disabled]="authService.loading()">
          <i class="fa-brands fa-google"></i>
          @if (authService.loading()) {
            Signing in...
          } @else {
            Sign in with Google
          }
        </sl-button>

        <sl-button (click)="signInWithMicrosoft()" [disabled]="authService.loading()">
          <i class="fa-brands fa-microsoft"></i>
          @if (authService.loading()) {
            Signing in...
          } @else {
            Sign in with Microsoft
          }
        </sl-button>

        <sl-button (click)="signInWithApple()" [disabled]="authService.loading()">
          <i class="fa-brands fa-apple"></i>
          @if (authService.loading()) {
            Signing in...
          } @else {
            Sign in with Apple
          }
        </sl-button>
      </div>
    </div>
  `,
  styles: [
    `
      .login-container {
        max-width: 400px;
        margin: 2rem auto;
      }

      .oauth-buttons {
        display: flex;
        flex-direction: column;
        gap: 1rem;
        margin-top: 2rem;
      }

      sl-button {
        width: 100%;
      }
    `,
  ],
})
export class LoginComponent {
  protected authService = inject(AuthService);
  private router = inject(Router);

  async signInWithGoogle() {
    try {
      await this.authService.signInWithOAuth('google');
      this.router.navigate(['/events']);
    } catch (error) {
      console.error('Google sign-in failed:', error);
    }
  }

  async signInWithMicrosoft() {
    try {
      await this.authService.signInWithOAuth('microsoft');
      this.router.navigate(['/events']);
    } catch (error) {
      console.error('Microsoft sign-in failed:', error);
    }
  }

  async signInWithApple() {
    try {
      await this.authService.signInWithOAuth('apple');
      this.router.navigate(['/events']);
    } catch (error) {
      console.error('Apple sign-in failed:', error);
    }
  }
}
```

## Navigation with Auth State

```typescript
// ✅ CORRECT: Show different nav based on auth
@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink, WebAwesomeModule],
  template: `
    <nav class="navbar">
      <div class="logo">
        <h1>Event Management</h1>
      </div>

      <div class="nav-links">
        @if (authService.isAuthenticated()) {
          <a routerLink="/events">Events</a>
          <a routerLink="/profile">Profile</a>

          <sl-dropdown>
            <sl-button slot="trigger">
              <i class="fa-solid fa-user"></i>
              {{ authService.currentUser()?.email }}
            </sl-button>
            <sl-menu>
              <sl-menu-item (click)="signOut()">
                <i class="fa-solid fa-sign-out-alt"></i> Sign Out
              </sl-menu-item>
            </sl-menu>
          </sl-dropdown>
        } @else {
          <a routerLink="/login">Sign In</a>
        }
      </div>
    </nav>
  `,
})
export class NavbarComponent {
  protected authService = inject(AuthService);
  private router = inject(Router);

  async signOut() {
    await this.authService.signOut();
    this.router.navigate(['/login']);
  }
}
```

## HTTP Interceptor with Auth

```typescript
// ✅ CORRECT: Add JWT token to all requests
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);

  const user = authService.currentUser();
  if (user) {
    // Add auth header if user is logged in
    req = req.clone({
      setHeaders: {
        Authorization: `Bearer ${user.user_metadata?.access_token || ''}`,
      },
    });
  }

  return next(req).pipe(
    catchError((error) => {
      if (error.status === 401) {
        // Token expired, refresh and retry
        return authService
          .refreshToken()
          .then(() => next(req))
          .catch(() => {
            authService.signOut();
            return throwError(() => error);
          });
      }
      return throwError(() => error);
    })
  );
};

// Register in app.config.ts
export const appConfig: ApplicationConfig = {
  providers: [provideHttpClient(withInterceptors([authInterceptor]))],
};
```

## Protected Data Service

```typescript
// ✅ CORRECT: Use auth in data service
@Injectable({ providedIn: 'root' })
export class UserService {
  private http = inject(HttpClient);
  private authService = inject(AuthService);

  private apiUrl = 'http://localhost:5000/api';

  getUserProfile(): Promise<UserProfile> {
    const userId = this.authService.currentUser()?.id;
    if (!userId) {
      return Promise.reject('User not authenticated');
    }

    return this.http
      .get<UserProfile>(`${this.apiUrl}/users/${userId}`)
      .toPromise()
      .then((data) => data || ({} as UserProfile))
      .catch(() => ({}) as UserProfile);
  }

  updateUserProfile(profile: Partial<UserProfile>): Promise<UserProfile> {
    const userId = this.authService.currentUser()?.id;
    if (!userId) {
      return Promise.reject('User not authenticated');
    }

    return this.http
      .patch<UserProfile>(`${this.apiUrl}/users/${userId}`, profile)
      .toPromise()
      .then((data) => data || ({} as UserProfile))
      .catch(() => ({}) as UserProfile);
  }
}
```

## Testing Auth

```typescript
describe('AuthService', () => {
  let service: AuthService;
  let supabase: SupabaseClient;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AuthService, SupabaseClient],
    });

    service = TestBed.inject(AuthService);
    supabase = TestBed.inject(SupabaseClient);
  });

  it('should initialize with no user', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.currentUser()).toBeNull();
  });

  it('should set user after successful login', async () => {
    const mockUser = { id: '123', email: 'test@example.com' };
    spyOn(supabase.auth, 'signInWithOAuth').and.returnValue(
      Promise.resolve({ data: { user: mockUser }, error: null })
    );

    await service.signInWithOAuth('google');
    expect(service.isAuthenticated()).toBe(true);
  });

  it('should clear user on sign out', async () => {
    service.currentUser.set({ id: '123', email: 'test@example.com' } as any);
    await service.signOut();
    expect(service.isAuthenticated()).toBe(false);
  });
});
```

## DO's and DON'Ts

### DO ✅

- Use Supabase OAuth for authentication
- Store user in signal
- Protect routes with guards
- Add auth token to all HTTP requests
- Handle token refresh on 401
- Log out on auth errors

### DON'T ❌

- Never store passwords
- Never store tokens in localStorage
- Never expose auth tokens in logs
- Never skip authorization checks
- Never trust client-side only auth
- Never use session storage for sensitive data
