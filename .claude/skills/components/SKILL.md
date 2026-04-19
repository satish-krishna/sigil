---
name: components
description: |
  Master Angular v21 standalone component architecture for Siora projects. Provides patterns for component design, dependency injection, smart/dumb component pattern, and OnPush change detection. Use this skill when: (1) Creating standalone components; (2) Using inject() for dependency injection; (3) Building presentational and container components; (4) Implementing OnPush change detection; (5) Managing component signals and lifecycle; (6) Testing component interactions
---

# Siora Angular Component Architecture

Modern standalone components with OnPush change detection and signals-first design.

## Quick Start: Standalone Component

**Before creating any component, find and read 2-3 similar existing components first.**

```bash
# Find existing components in the same feature area
find libs/frontend/features -name "*.component.ts" | head -20
```

```typescript
// ✅ CORRECT: Modern standalone component using Spartan-NG
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
import { HlmButtonDirective } from '@siora/helm/button';
import { HlmCardDirective, HlmCardContentDirective } from '@siora/helm/card';

@Component({
  selector: 'siora-event-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, HlmButtonDirective, HlmCardDirective, HlmCardContentDirective],
  template: `
    <div hlmCard class="mb-4">
      <div hlmCardContent class="p-4">
        <h3 class="text-lg font-semibold">{{ event.name }}</h3>
        <p class="text-muted-foreground">
          <i class="fa-solid fa-calendar mr-2"></i>{{ event.date }}
        </p>
        <div class="flex gap-2 mt-3">
          <button hlmBtn variant="outline" size="sm" (click)="onEdit.emit(event)">
            <i class="fa-solid fa-pen mr-2"></i>Edit
          </button>
          <button hlmBtn variant="destructive" size="sm" (click)="onDelete.emit(event.id)">
            <i class="fa-solid fa-trash mr-2"></i>Delete
          </button>
        </div>
      </div>
    </div>
  `,
})
export class EventCardComponent {
  @Input() event!: Event;

  onEdit = output<Event>();
  onDelete = output<number>();
}
```

## Dependency Injection with inject()

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
    />
  `,
})
export class EventsPageComponent {
  private store = inject(EventStore);
  private eventService = inject(EventService);

  async onSelectEvent(id: number) {
    this.store.selectEvent(id);
  }
}

// PRESENTATIONAL: Dumb component, only renders
@Component({
  selector: 'app-event-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <p>Loading...</p>
    } @else {
      @for (event of events(); track event.id) {
        <app-event-card [event]="event" />
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

## OnPush Change Detection

```typescript
// ✅ CORRECT: Always use OnPush for performance
@Component({
  selector: 'app-event-summary',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="summary">
      <h3>{{ title() }}</h3>
      <p>Total: {{ count() }}</p>
    </div>
  `,
})
export class EventSummaryComponent {
  @Input() events!: Signal<Event[]>;

  title = computed(() => `Events (${this.events().length})`);
  count = computed(() => this.events().length);
}
```

## Component Signals

```typescript
// ✅ CORRECT: Use signals for component state
@Component({
  selector: 'app-event-filter',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <input (change)="searchQuery.set($event.target.value)" />
    <p>Showing {{ filteredCount() }} events</p>
  `,
})
export class EventFilterComponent {
  private store = inject(EventStore);

  searchQuery = signal('');
  filteredCount = computed(() => {
    const query = this.searchQuery();
    return this.store.events().filter((e) => !query || e.name.includes(query)).length;
  });
}
```

## Complete Reference

See [components-patterns.md](references/components-patterns.md) for detailed patterns including:

- Component with Resource API
- Smart/dumb pattern examples
- Testing component interactions
- Lifecycle hook patterns
- DO's and DON'Ts checklist

## Key Principles

✅ **DO**

- Use `standalone: true` for all components
- Always use `ChangeDetectionStrategy.OnPush`
- Use `inject()` for dependency injection
- Use signals for component state
- Create presentational and container components

❌ **DON'T**

- Never use NgModule
- Never use default change detection
- Never use constructor injection
- Never mutate signals directly
- Never put business logic in presentational components
