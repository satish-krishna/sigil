---
name: resource-api
description: |
  Master Angular Resource API for async data loading with Promises (NO RxJS). Provides patterns for automatic data fetching, reactive parameters, and error handling. Use this skill when: (1) Loading async data with automatic refetch; (2) Handling loading, error, and value states; (3) Creating resources with reactive parameters; (4) Building components that display async data; (5) Testing Resource API with HttpClientTestingModule
---

# Siora Resource API Patterns

Angular Resource API is Siora's primary async data fetching mechanism (Promises only, NO RxJS Observables).

## Quick Start: Basic Resource

```typescript
// ✅ CORRECT: Resource API for automatic async loading
import { Injectable, inject, resource } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Injectable({ providedIn: 'root' })
export class EventService {
  private http = inject(HttpClient);
  private apiUrl = 'http://localhost:5000/api/events';

  // Resource automatically loads on init and when params change
  eventsResource = resource({
    loader: () => this.fetchEvents(),
    defaultValue: [],
  });

  private fetchEvents(): Promise<Event[]> {
    return this.http
      .get<Event[]>(this.apiUrl)
      .toPromise()
      .then((data) => data || [])
      .catch((err) => {
        console.error('Failed to fetch events:', err);
        return [];
      });
  }
}
```

## Using Resources in Components

```typescript
// ✅ CORRECT: Resource API with type guards
@Component({
  selector: 'app-events',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (eventService.eventsResource.isLoading()) {
      <p><i class="fa-solid fa-spinner fa-spin"></i> Loading...</p>
    } @else if (eventService.eventsResource.hasError()) {
      <div class="error">
        <i class="fa-solid fa-triangle-exclamation"></i>
        Error: {{ eventService.eventsResource.error() }}
      </div>
    } @else if (eventService.eventsResource.hasValue()) {
      @for (event of eventService.eventsResource.value(); track event.id) {
        <app-event-card [event]="event" />
      }
    }
  `,
})
export class EventsComponent {
  protected eventService = inject(EventService);

  reload() {
    this.eventService.eventsResource.reload();
  }
}
```

## Resource with Reactive Parameters

```typescript
// ✅ CORRECT: Resource that refetches when params change
@Injectable({ providedIn: 'root' })
export class EventService {
  private http = inject(HttpClient);

  // Signal to control fetching
  selectedVendorId = signal<number | null>(null);

  // Resource that depends on selectedVendorId
  eventsByVendorResource = resource({
    params: () => ({
      vendorId: this.selectedVendorId()
    }),
    loader: ({ params }) => {
      if (!params.vendorId) return Promise.resolve([]);
      return this.fetchEventsByVendor(params.vendorId);
    },
    defaultValue: []
  });

  private fetchEventsByVendor(vendorId: number): Promise<Event[]> {
    return this.http.get<Event[]>(
      `/api/vendors/${vendorId}/events`
    ).toPromise()
      .then(data => data || [])
      .catch(() => []);
  }
}

// In component: just change the signal!
selectVendor(vendorId: number) {
  this.eventService.selectedVendorId.set(vendorId);
  // Resource automatically refetches!
}
```

## Resource States

```typescript
// ✅ CORRECT: Checking resource states
eventsResource = resource({
  loader: () => this.fetchEvents(),
  defaultValue: []
});

// Use type guards to safely access values
@if (eventsResource.isLoading()) {
  // isLoading() === true when fetching
}
@if (eventsResource.hasError()) {
  // hasError() === true if fetch failed
  const error = eventsResource.error(); // Access error safely
}
@if (eventsResource.hasValue()) {
  // hasValue() === true if fetch succeeded
  const events = eventsResource.value(); // Safe to access now
}
```

## Multiple Resources

```typescript
// ✅ CORRECT: Multiple resources with different endpoints
@Injectable({ providedIn: 'root' })
export class DataService {
  private http = inject(HttpClient);

  eventsResource = resource({
    loader: () =>
      this.http
        .get<Event[]>('/api/events')
        .toPromise()
        .then((d) => d || [])
        .catch(() => []),
    defaultValue: [],
  });

  vendorsResource = resource({
    loader: () =>
      this.http
        .get<Vendor[]>('/api/vendors')
        .toPromise()
        .then((d) => d || [])
        .catch(() => []),
    defaultValue: [],
  });
}
```

## Complete Reference

See [resource-api-patterns.md](references/resource-api-patterns.md) for detailed patterns including:

- Resource with manual reload
- Error handling strategies
- Loading state signals
- Testing Resource API
- DO's and DON'Ts checklist

## Key Principles

✅ **DO**

- Use `resource()` for all async data
- Use `.toPromise()` to convert observables to promises
- Use type guards: `hasValue()`, `hasError()`, `isLoading()`
- Call `.reload()` to manually refetch
- Return default values from loaders

❌ **DON'T**

- Never subscribe to resources
- Never use observable patterns
- Never forget type guards before accessing `.value()`
- Never ignore error handling
- Never use async/await in templates
