# Component Architecture Patterns

Complete patterns for Angular v21 standalone components.

## Standalone Component Template

```typescript
// ✅ CORRECT: Modern standalone component with Spartan-NG
import {
  Component,
  inject,
  signal,
  computed,
  ChangeDetectionStrategy,
  Input,
  output,
} from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-event-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule],
  template: `
    <div hlmCard class="p-4">
      <div class="flex items-start justify-between">
        <div>
          <h3 class="font-semibold text-lg">{{ event.name }}</h3>
          <p class="text-sm text-muted-foreground mt-2">
            <i class="fa-solid fa-calendar mr-2"></i>
            {{ event.date }}
          </p>
          <p class="text-sm text-muted-foreground mt-1">
            <i class="fa-solid fa-map-pin mr-2"></i>
            {{ event.location }}
          </p>
        </div>
        <i class="fa-solid fa-bookmark text-primary"></i>
      </div>

      <div class="flex gap-2 mt-4">
        <button hlmBtn variant="primary" (click)="onEdit.emit(event)" class="flex-1">
          <i class="fa-solid fa-edit mr-2"></i>
          Edit
        </button>
        <button hlmBtn variant="destructive" (click)="onDelete.emit(event.id)" class="flex-1">
          <i class="fa-solid fa-trash mr-2"></i>
          Delete
        </button>
      </div>
    </div>
  `,
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

// PRESENTATIONAL: Dumb component, only renders with Spartan-NG
@Component({
  selector: 'app-event-list',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <div class="flex items-center justify-center py-8">
        <i class="fa-solid fa-spinner fa-spin text-2xl text-primary"></i>
      </div>
    } @else if (events().length === 0) {
      <div class="text-center py-8">
        <i class="fa-solid fa-calendar text-4xl text-muted-foreground mb-4"></i>
        <p class="text-muted-foreground">No events found</p>
      </div>
    } @else {
      <div class="space-y-4">
        @for (event of events(); track event.id) {
          <app-event-card [event]="event" (onEdit)="select.emit(event.id)" />
        }
      </div>
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
    <div class="space-y-4">
      <input
        hlmInput
        type="text"
        (input)="searchQuery.set($event.target.value)"
        placeholder="Search events..."
        class="w-full"
      />

      <select hlmSelect (change)="selectedVendor.set($event.target.value)" class="w-full">
        <option value="">All Vendors</option>
        @for (vendor of vendors(); track vendor.id) {
          <option [value]="vendor.id">{{ vendor.name }}</option>
        }
      </select>

      <div class="text-sm text-muted-foreground flex items-center gap-2">
        <i class="fa-solid fa-filter"></i>
        Showing {{ filteredCount() }} of {{ totalCount() }} events
      </div>
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
      <div class="flex items-center justify-center py-8">
        <i class="fa-solid fa-spinner fa-spin text-2xl text-primary mr-3"></i>
        <span>Loading...</span>
      </div>
    } @else if (eventResource.hasError()) {
      <div hlmAlert variant="destructive">
        <i class="fa-solid fa-circle-exclamation mr-2"></i>
        {{ eventResource.error()?.message }}
      </div>
    } @else if (eventResource.hasValue()) {
      @let event = eventResource.value();
      <div hlmCard class="p-6">
        <div class="flex items-start justify-between mb-4">
          <div>
            <h2 class="text-2xl font-bold">{{ event.name }}</h2>
            <p class="text-muted-foreground mt-1">{{ event.date }}</p>
          </div>
          <button hlmBtn variant="outline" size="icon" (click)="onEdit()">
            <i class="fa-solid fa-edit"></i>
          </button>
        </div>
        <p class="text-gray-700 dark:text-gray-300">{{ event.description }}</p>
      </div>
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
