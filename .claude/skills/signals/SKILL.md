---
name: signals
description: |
  Master Angular Signals state management for Siora projects. Provides comprehensive patterns for creating reactive state with signals, computed properties, and signal effects. Use this skill when: (1) Building stores for component state; (2) Creating reactive derived state with computed(); (3) Managing global application state; (4) Testing signal-based services and components; (5) Implementing complex state mutations with update() and set()
---

# Siora Angular Signals Patterns

Angular Signals are the core of Siora's state management (NO RxJS). This skill provides proven patterns for signals, computed properties, and signal-based stores.

## Quick Start: Basic Signal Store

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
```

## Using Signals in Components

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
```

## Signal Mutations

```typescript
// ✅ set() - Replace entire value
store.events.set([newEvent]);

// ✅ update() - Modify based on current value
store.events.update((current) => [...current, newEvent]);

// ✅ update() - For partial object updates (mutate() was removed in Angular 17)
store.user.update((u) => ({ ...u, name: 'New Name', email: 'new@email.com' }));
```

## Computed Signals

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

## Signal Effects (Advanced)

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
        console.log('Event selected:', id);
      }
    });

    // Run when multiple signals change
    effect(() => {
      const events = this.events();
      const count = events.length;
      document.title = `Events (${count})`;
    });
  }
}
```

## Complete Reference

See [signals-patterns.md](references/signals-patterns.md) for detailed patterns including:

- Testing signals with Jest/Spectator
- Advanced computed patterns
- Error handling strategies
- Performance optimization tips
- DO's and DON'Ts checklist

## Key Principles

✅ **DO**

- Use `signal()` for all state
- Use `computed()` for derived state
- Inject stores in components
- Call signals like functions: `store.events()`
- Use `update()` for conditional changes
- Use `set()` for full replacement

❌ **DON'T**

- Never use `BehaviorSubject` or any RxJS
- Never mutate signals directly: `store.events().push()` won't work
- Never subscribe to anything
- Never use NgRx or any state library
- Never use subjects or observables
- Never forget to call signals: `store.events` is the signal object, `store.events()` is the value
