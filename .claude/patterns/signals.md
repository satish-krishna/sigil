# Signals State Management Patterns

Complete patterns for Angular Signals in Siora.

## Basic Signal Store

```typescript
// ✅ CORRECT: Signals-based store
import { Injectable, signal, computed } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class EventStore {
  // Writable signals
  events = signal<Event[]>([]);
  selectedEventId = signal<number | null>(null);
  loading = signal(false);
  error = signal<string | null>(null);

  // Computed signals (automatically update when dependencies change)
  selectedEvent = computed(() => {
    const id = this.selectedEventId();
    return this.events().find((e) => e.id === id);
  });

  filteredEvents = computed(() => {
    return this.events().filter((e) => !e.cancelled);
  });

  eventCount = computed(() => this.events().length);

  // Methods to update signals
  setEvents(events: Event[]) {
    this.events.set(events);
  }

  addEvent(event: Event) {
    this.events.update((current) => [...current, event]);
  }

  selectEvent(id: number) {
    this.selectedEventId.set(id);
  }

  clearError() {
    this.error.set(null);
  }
}

// ❌ WRONG: Using subjects or BehaviorSubject
// export class EventStore {
//   events$ = new BehaviorSubject<Event[]>([]);
//   events = this.events$.asObservable();
// }
```

## In Components

```typescript
// ✅ CORRECT: Inject store and use signals directly
@Component({
  selector: 'app-event-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule],
  template: `
    <div class="events">
      @if (store.loading()) {
        <p>Loading...</p>
      } @else if (store.error()) {
        <p>Error: {{ store.error() }}</p>
      } @else {
        @for (event of store.filteredEvents(); track event.id) {
          <app-event-card [event]="event" />
        }
      }
    </div>
  `,
})
export class EventListComponent {
  protected store = inject(EventStore);
  // No need for ngOnInit or unsubscribe!
}

// ❌ WRONG: Subscribing to observables
// export class EventListComponent {
//   events$ = this.store.events$;
//   ngOnInit() {
//     this.events$.subscribe(events => {
//       this.events.set(events);
//     });
//   }
// }
```

## Signal Mutations

```typescript
// ✅ set() - Replace entire value
store.events.set([newEvent]);

// ✅ update() - Modify based on current value
store.events.update((current) => [...current, newEvent]);

// ✅ mutate() - For complex objects
store.user.mutate((u) => {
  u.name = 'New Name';
  u.email = 'new@email.com';
});

// ❌ WRONG: Direct mutation (doesn't trigger updates)
// store.events().push(newEvent); // This won't work!
```

## Derived Signals (Computed)

```typescript
// ✅ Automatically updates when dependencies change
eventsByVendor = computed(() => {
  const vendorId = this.selectedVendorId();
  return this.events().filter((e) => e.vendorId === vendorId);
});

// Multiple levels of computed signals
filteredAndSorted = computed(() => {
  const filtered = this.filteredEvents();
  return filtered.sort((a, b) => a.date.localeCompare(b.date));
});

// Count derived from filtered data
filteredCount = computed(() => this.filteredAndSorted().length);
```

## Global State Pattern

```typescript
// ✅ Single-file store for entire app state
import { Injectable, signal, computed } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AppState {
  // Auth
  currentUser = signal<User | null>(null);
  isAuthenticated = computed(() => !!this.currentUser());

  // Events
  events = signal<Event[]>([]);
  selectedEventId = signal<number | null>(null);

  // UI
  sidebarOpen = signal(true);
  loading = signal(false);

  // Computed
  selectedEvent = computed(() => {
    const id = this.selectedEventId();
    return this.events().find(e => e.id === id);
  });

  // Methods
  login(user: User) {
    this.currentUser.set(user);
  }

  logout() {
    this.currentUser.set(null);
  }

  toggleSidebar() {
    this.sidebarOpen.update(v => !v);
  }
}

// In any component
@Component({...})
export class MyComponent {
  appState = inject(AppState);

  // Use anywhere
  get isLoggedIn() {
    return this.appState.isAuthenticated();
  }
}
```

## Signal Effects (Optional)

```typescript
// ✅ Side effects that run when signals change
// Use sparingly - prefer derived signals when possible
import { effect } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class EventStore {
  events = signal<Event[]>([]);
  selectedEventId = signal<number | null>(null);

  constructor() {
    // Run code when selectedEventId changes
    effect(() => {
      const id = this.selectedEventId();
      if (id) {
        // Log or trigger side effect
        console.log('Event selected:', id);
      }
    });

    // Run when multiple signals change
    effect(() => {
      const events = this.events();
      const count = events.length;
      // Update document title, analytics, etc.
      document.title = `Events (${count})`;
    });
  }
}
```

## DO's and DON'Ts

### DO ✅

- Use `signal()` for all state
- Use `computed()` for derived state
- Inject stores in components
- Call signals like functions: `store.events()`
- Use `update()` for conditional changes
- Use `set()` for full replacement

### DON'T ❌

- Never use `BehaviorSubject` or any RxJS
- Never mutate signals directly: `store.events().push()` won't work
- Never subscribe to anything
- Never use NgRx or any state library
- Never use subjects or observables
- Never forget to call signals: `store.events` is the signal object, `store.events()` is the value

## Testing Signals

```typescript
describe('EventStore', () => {
  let store: EventStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [EventStore],
    });
    store = TestBed.inject(EventStore);
  });

  it('should set events', () => {
    store.setEvents([{ id: 1, name: 'Event 1' }]);
    expect(store.events().length).toBe(1);
  });

  it('should select event', () => {
    store.setEvents([{ id: 1, name: 'Event 1' }]);
    store.selectEvent(1);
    expect(store.selectedEvent()?.name).toBe('Event 1');
  });

  it('should compute filtered events', () => {
    store.setEvents([
      { id: 1, name: 'Event 1', cancelled: false },
      { id: 2, name: 'Event 2', cancelled: true },
    ]);
    expect(store.filteredEvents().length).toBe(1);
  });
});
```
