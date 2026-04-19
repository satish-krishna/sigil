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
// ✅ CORRECT: OAuth login buttons with Spartan-NG
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="max-w-md mx-auto mt-8 px-4">
      <div hlmCard class="p-8">
        <h1 class="text-2xl font-bold mb-6 flex items-center gap-2">
          <i class="fa-solid fa-sign-in-alt text-primary"></i>
          Sign In
        </h1>

        @if (authService.error()) {
          <div hlmAlert variant="destructive" class="mb-6">
            <i class="fa-solid fa-circle-exclamation mr-2"></i>
            {{ authService.error() }}
          </div>
        }

        <div class="space-y-3">
          <button
            hlmBtn
            variant="outline"
            class="w-full"
            (click)="signInWithGoogle()"
            [disabled]="authService.loading()"
          >
            <i class="fa-brands fa-google mr-2"></i>
            @if (authService.loading()) {
              <i class="fa-solid fa-spinner fa-spin mr-2"></i>
              Signing in...
            } @else {
              Sign in with Google
            }
          </button>

          <button
            hlmBtn
            variant="outline"
            class="w-full"
            (click)="signInWithMicrosoft()"
            [disabled]="authService.loading()"
          >
            <i class="fa-brands fa-microsoft mr-2"></i>
            @if (authService.loading()) {
              <i class="fa-solid fa-spinner fa-spin mr-2"></i>
              Signing in...
            } @else {
              Sign in with Microsoft
            }
          </button>

          <button
            hlmBtn
            variant="outline"
            class="w-full"
            (click)="signInWithApple()"
            [disabled]="authService.loading()"
          >
            <i class="fa-brands fa-apple mr-2"></i>
            @if (authService.loading()) {
              <i class="fa-solid fa-spinner fa-spin mr-2"></i>
              Signing in...
            } @else {
              Sign in with Apple
            }
          </button>
        </div>
      </div>
    </div>
  `,
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
// ✅ CORRECT: Show different nav based on auth with Spartan-NG
@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <nav class="border-b bg-white dark:bg-slate-950">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex items-center justify-between h-16">
          <div class="flex items-center gap-2">
            <i class="fa-solid fa-calendar-days text-2xl text-primary"></i>
            <h1 class="text-xl font-bold">Siora</h1>
          </div>

          <div class="flex items-center gap-6">
            @if (authService.isAuthenticated()) {
              <a routerLink="/events" class="text-sm font-medium hover:text-primary transition">
                <i class="fa-solid fa-list mr-1"></i>
                Events
              </a>
              <a routerLink="/vendors" class="text-sm font-medium hover:text-primary transition">
                <i class="fa-solid fa-store mr-1"></i>
                Vendors
              </a>
              <a routerLink="/profile" class="text-sm font-medium hover:text-primary transition">
                <i class="fa-solid fa-user mr-1"></i>
                Profile
              </a>

              <div class="relative group">
                <button hlmBtn variant="ghost" size="sm">
                  <i class="fa-solid fa-user-circle"></i>
                  {{ authService.currentUser()?.email }}
                </button>

                <div
                  class="absolute right-0 mt-0 w-48 bg-white dark:bg-slate-950 rounded-md shadow-lg border hidden group-hover:block z-50"
                >
                  <a
                    routerLink="/profile"
                    class="block px-4 py-2 hover:bg-slate-100 dark:hover:bg-slate-800"
                  >
                    <i class="fa-solid fa-gear mr-2"></i>
                    Settings
                  </a>
                  <button
                    (click)="signOut()"
                    class="w-full text-left px-4 py-2 hover:bg-slate-100 dark:hover:bg-slate-800 text-destructive"
                  >
                    <i class="fa-solid fa-sign-out-alt mr-2"></i>
                    Sign Out
                  </button>
                </div>
              </div>
            } @else {
              <a routerLink="/login" hlmBtn variant="primary">
                <i class="fa-solid fa-sign-in-alt mr-2"></i>
                Sign In
              </a>
            }
          </div>
        </div>
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
