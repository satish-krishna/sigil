---
name: authentication
description: |
  Master Supabase OAuth authentication for Siora projects. Provides patterns for OAuth sign-in, route guards, HTTP interceptors, and auth state management. Use this skill when: (1) Implementing OAuth sign-in (Google, Microsoft, Apple, Facebook); (2) Creating route guards for protected pages; (3) Managing auth tokens in HTTP requests; (4) Handling token refresh on 401; (5) Implementing sign-out and session management; (6) Testing authentication flows
---

# Siora Authentication Patterns

OAuth-based authentication with Supabase using signals for state management.

## Quick Start: Auth Service

```typescript
// ✅ CORRECT: Supabase OAuth authentication
import { Injectable, inject, signal, computed } from '@angular/core';
import { SupabaseClient } from '@supabase/supabase-js';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private supabase = inject(SupabaseClient);

  currentUser = signal<User | null>(null);
  loading = signal(false);
  error = signal<string | null>(null);

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

      if (error) throw new Error(error.message);
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
          Sign in with Google
        </sl-button>

        <sl-button (click)="signInWithMicrosoft()" [disabled]="authService.loading()">
          <i class="fa-brands fa-microsoft"></i>
          Sign in with Microsoft
        </sl-button>
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
      console.error('Sign-in failed:', error);
    }
  }

  async signInWithMicrosoft() {
    try {
      await this.authService.signInWithOAuth('microsoft');
      this.router.navigate(['/events']);
    } catch (error) {
      console.error('Sign-in failed:', error);
    }
  }
}
```

## HTTP Interceptor

```typescript
// ✅ CORRECT: Add JWT token to all requests
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);

  const user = authService.currentUser();
  if (user) {
    req = req.clone({
      setHeaders: {
        Authorization: `Bearer ${user.user_metadata?.access_token || ''}`,
      },
    });
  }

  return next(req).pipe(
    catchError((error) => {
      if (error.status === 401) {
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

## Complete Reference

See [authentication-patterns.md](references/authentication-patterns.md) for detailed patterns including:

- Navigation with auth state
- Protected data service
- Testing authentication
- Error handling
- DO's and DON'Ts checklist

## Key Principles

✅ **DO**

- Use Supabase OAuth for authentication
- Store user in signal
- Protect routes with guards
- Add auth token to HTTP requests
- Handle token refresh on 401
- Log out on auth errors

❌ **DON'T**

- Never store passwords
- Never store tokens in localStorage
- Never expose auth tokens in logs
- Never skip authorization checks
- Never trust client-side only auth
