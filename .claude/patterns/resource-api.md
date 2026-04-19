# Resource API Patterns (Async Data Fetching)

Complete patterns for Angular Resource API in Siora - NO RxJS, Promises only!

## Basic Resource (Automatic Fetching)

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

// ❌ WRONG: Observable patterns
// export class EventService {
//   eventsResource = this.http.get<Event[]>(this.apiUrl);
// }
```

## In Components

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

// ❌ WRONG: Subscribe pattern
// export class EventsComponent {
//   events$ = this.eventService.eventsResource;
//   ngOnInit() {
//     this.events$.subscribe(events => { ... });
//   }
// }
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

// Resource also provides:
eventsResource.value()        // The actual data
eventsResource.error()        // Error object if failed
eventsResource.isLoading()    // boolean
eventsResource.hasValue()     // boolean
eventsResource.hasError()     // boolean
eventsResource.reload()       // Manual refetch
```

## Multiple Resources in Service

```typescript
// ✅ CORRECT: Multiple resources with different endpoints
@Injectable({ providedIn: 'root' })
export class DataService {
  private http = inject(HttpClient);

  // List resources
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

  // Detail resource with parameters
  eventId = signal<number | null>(null);

  eventDetailResource = resource({
    params: () => this.eventId(),
    loader: ({ params }) => {
      if (!params) return Promise.resolve(null);
      return this.http
        .get<Event>(`/api/events/${params}`)
        .toPromise()
        .catch(() => null);
    },
    defaultValue: null as Event | null,
  });
}
```

## Resource with Manual Reload

```typescript
// ✅ CORRECT: Manual reload button
@Component({
  template: `
    <button (click)="refresh()"><i class="fa-solid fa-rotate-right"></i> Refresh</button>

    @if (eventsResource.isLoading()) {
      <p>Loading...</p>
    } @else if (eventsResource.hasValue()) {
      @for (event of eventsResource.value(); track event.id) {
        <p>{{ event.name }}</p>
      }
    }
  `,
})
export class EventsComponent {
  private eventService = inject(EventService);
  protected eventsResource = this.eventService.eventsResource;

  refresh() {
    this.eventsResource.reload();
  }
}
```

## Resource with Loading State Signal

```typescript
// ✅ CORRECT: Combine resource with signal for UI state
@Injectable({ providedIn: 'root' })
export class EventStore {
  private eventService = inject(EventService);

  eventsResource = this.eventService.eventsResource;
  isLoadingMore = signal(false);

  async loadMore() {
    this.isLoadingMore.set(true);
    try {
      await new Promise(resolve => setTimeout(resolve, 1000));
      // Load more data
    } finally {
      this.isLoadingMore.set(false);
    }
  }
}

// In component
@if (store.eventsResource.isLoading() || store.isLoadingMore()) {
  <p>Loading...</p>
}
```

## Error Handling

```typescript
// ✅ CORRECT: Proper error handling in resource
private fetchEvents(): Promise<Event[]> {
  return this.http.get<Event[]>('/api/events')
    .toPromise()
    .then(data => {
      if (!data) {
        throw new Error('No data returned');
      }
      return data;
    })
    .catch(error => {
      // Log error but don't crash
      console.error('Failed to fetch events:', error);
      // Return default value or rethrow
      return [];
    });
}

// In component: safely check for errors
@if (eventsResource.hasError()) {
  <div class="error-banner">
    <i class="fa-solid fa-circle-exclamation"></i>
    {{ eventsResource.error()?.message || 'Failed to load events' }}
    <button (click)="eventsResource.reload()">Retry</button>
  </div>
}
```

## Testing Resource API

```typescript
describe('EventService', () => {
  let service: EventService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [EventService],
      imports: [HttpClientTestingModule],
    });
    service = TestBed.inject(EventService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('should load events via resource', () => {
    service.eventsResource.reload();

    const req = httpMock.expectOne('/api/events');
    req.flush([{ id: 1, name: 'Event 1' }]);

    // Check resource state
    expect(service.eventsResource.hasValue()).toBe(true);
    expect(service.eventsResource.value().length).toBe(1);
  });

  it('should handle errors in resource', () => {
    service.eventsResource.reload();

    const req = httpMock.expectOne('/api/events');
    req.error(new ErrorEvent('Network error'));

    // Check error state
    expect(service.eventsResource.hasError()).toBe(true);
  });

  afterEach(() => {
    httpMock.verify();
  });
});
```

## DO's and DON'Ts

### DO ✅

- Use `resource()` for all async data
- Use `.toPromise()` to convert observables to promises
- Use type guards: `hasValue()`, `hasError()`, `isLoading()`
- Call `.reload()` to manually refetch
- Return default values from loaders

### DON'T ❌

- Never subscribe to resources
- Never use observable patterns
- Never forget type guards before accessing `.value()`
- Never ignore error handling
- Never use async/await in templates (use resource instead)
