# Component Architecture Patterns

Complete patterns for Angular v21 standalone components.

## Standalone Component Template

```typescript
// ✅ CORRECT: Modern standalone component
import { Component, inject, signal, computed, ChangeDetectionStrategy, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-event-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, WebAwesomeModule, CommonModule],
  template: `
    <sl-card class="event-card">
      <h3>{{ event.name }}</h3>
      <p>
        <i class="fa-solid fa-calendar"></i>
        {{ event.date }}
      </p>
      <p>
        <i class="fa-solid fa-map-pin"></i>
        {{ event.location }}
      </p>

      <div class="actions">
        <sl-button (click)="onEdit.emit(event)"> <i class="fa-solid fa-edit"></i> Edit </sl-button>
        <sl-button variant="danger" (click)="onDelete.emit(event.id)">
          <i class="fa-solid fa-trash"></i> Delete
        </sl-button>
      </div>
    </sl-card>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .event-card {
        padding: 1rem;
        margin-bottom: 1rem;
      }

      .actions {
        display: flex;
        gap: 0.5rem;
        margin-top: 1rem;
      }
    `,
  ],
})
export class EventCardComponent {
  @Input() event!: Event;

  onEdit = output<Event>();
  onDelete = output<number>();
}

// ❌ WRONG: Class-based components
// export class EventCardComponent {
//   @ViewChild(...) ...
//   ngOnInit() { ... }
// }
```

## Component with Dependency Injection

```typescript
// ✅ CORRECT: Use inject() for DI
@Component({...})
export class EventListComponent {
  private eventStore = inject(EventStore);
  private router = inject(Router);
  private eventService = inject(EventService);

  events = this.eventStore.events;
  loading = this.eventStore.loading;

  selectEvent(id: number) {
    this.eventStore.selectEvent(id);
    this.router.navigate(['/events', id]);
  }
}

// ❌ WRONG: Constructor injection
// export class EventListComponent {
//   constructor(private eventStore: EventStore) {}
// }
```

## Container vs Presentational Components

```typescript
// CONTAINER: Smart component with logic
@Component({
  selector: 'app-events-page',
  standalone: true,
  imports: [CommonModule, EventListComponent],
  template: `
    <app-event-list
      [events]="store.filteredEvents()"
      [loading]="store.loading()"
      (select)="onSelectEvent($event)"
      (delete)="onDeleteEvent($event)"
    />
  `,
})
export class EventsPageComponent {
  private store = inject(EventStore);
  private eventService = inject(EventService);

  async onSelectEvent(id: number) {
    this.store.selectEvent(id);
  }

  async onDeleteEvent(id: number) {
    await this.eventService.deleteEvent(id);
    this.store.removeEvent(id);
  }
}

// PRESENTATIONAL: Dumb component, only renders
@Component({
  selector: 'app-event-list',
  standalone: true,
  imports: [CommonModule, WebAwesomeModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <p>Loading...</p>
    } @else if (events().length === 0) {
      <p>No events found</p>
    } @else {
      @for (event of events(); track event.id) {
        <app-event-card [event]="event" (click)="select.emit(event.id)" />
      }
    }
  `,
})
export class EventListComponent {
  @Input() events!: Signal<Event[]>;
  @Input() loading!: Signal<boolean>;

  select = output<number>();
}
```

## Component with Signals and Computed

```typescript
// ✅ CORRECT: Use signals for component state
@Component({
  selector: 'app-event-filter',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="filters">
      <sl-input
        (change)="searchQuery.set($event.target.value)"
        placeholder="Search events..."
      ></sl-input>

      <sl-select (change)="selectedVendor.set($event.target.value)">
        <sl-option value="">All Vendors</sl-option>
        @for (vendor of vendors(); track vendor.id) {
          <sl-option [value]="vendor.id">{{ vendor.name }}</sl-option>
        }
      </sl-select>

      <p>Showing {{ filteredCount() }} of {{ totalCount() }} events</p>
    </div>
  `,
})
export class EventFilterComponent {
  private store = inject(EventStore);

  searchQuery = signal('');
  selectedVendor = signal<number | null>(null);

  vendors = this.store.vendors;
  events = this.store.events;

  filteredCount = computed(() => {
    const query = this.searchQuery();
    const vendor = this.selectedVendor();

    return this.events().filter(
      (e) => (!query || e.name.includes(query)) && (!vendor || e.vendorId === vendor)
    ).length;
  });

  totalCount = computed(() => this.events().length);
}
```

## Component with Resource API

```typescript
// ✅ CORRECT: Use resource in component
@Component({
  selector: 'app-event-detail',
  standalone: true,
  imports: [CommonModule, WebAwesomeModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (eventResource.isLoading()) {
      <p><i class="fa-solid fa-spinner fa-spin"></i> Loading...</p>
    } @else if (eventResource.hasError()) {
      <p>Error: {{ eventResource.error() }}</p>
    } @else if (eventResource.hasValue()) {
      @let event = eventResource.value();
      <sl-card>
        <h2>{{ event.name }}</h2>
        <p>{{ event.description }}</p>
        <sl-button (click)="onEdit()"> <i class="fa-solid fa-edit"></i> Edit </sl-button>
      </sl-card>
    }
  `,
})
export class EventDetailComponent {
  private eventService = inject(EventService);
  private route = inject(ActivatedRoute);

  eventId = toSignal(this.route.paramMap.pipe(map((p) => +p.get('id')!)));

  eventResource = resource({
    params: () => this.eventId(),
    loader: ({ params }) => {
      if (!params) return Promise.resolve(null);
      return this.eventService.getEvent(params);
    },
    defaultValue: null,
  });
}
```

## Component with OnPush Change Detection

```typescript
// ✅ CORRECT: Always use OnPush for performance
@Component({
  selector: 'app-event-summary',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="summary">
      <h3>{{ title() }}</h3>
      <p>Total events: {{ count() }}</p>
      <p>Upcoming: {{ upcomingCount() }}</p>
    </div>
  `,
})
export class EventSummaryComponent {
  @Input() events!: Signal<Event[]>;

  title = computed(() => `Events (${this.events().length})`);
  count = computed(() => this.events().length);
  upcomingCount = computed(() => this.events().filter((e) => new Date(e.date) > new Date()).length);
}

// ❌ WRONG: Default change detection
// changeDetection: ChangeDetectionStrategy.Default
```

## DO's and DON'Ts

### DO ✅

- Use `standalone: true` for all components
- Always use `ChangeDetectionStrategy.OnPush`
- Use `inject()` for dependency injection
- Use signals for component state
- Create presentational and container components
- Keep components small and focused

### DON'T ❌

- Never use NgModule
- Never use default change detection
- Never use constructor injection
- Never mutate signals directly
- Never use ViewChild for state
- Never put business logic in presentational components
