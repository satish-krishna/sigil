# Spartan-NG Components + Tailwind CSS Patterns

Complete patterns for Spartan-NG UI components with Tailwind CSS utility styling and Font Awesome Pro icons.

## Overview

Spartan-NG provides headless, accessible UI components as Angular directives that you style with Tailwind CSS. Combined with Font Awesome Pro icons, this creates a modern, flexible UI system with minimal overhead.

**Key Features:**

- Directives for all UI components (buttons, cards, alerts, inputs, selects, etc.)
- Styled with Tailwind CSS utilities (no CSS-in-JS)
- Font Awesome Pro icons for rich iconography
- Accessible by default
- Works on web, iOS, Android via Capacitor
- Platform-specific styling support (iOS, Android, Web)

---

## Button Component

### Basic Button

```typescript
// ✅ CORRECT: Basic Spartan-NG button with Tailwind styling
@Component({
  selector: 'app-button-demo',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!-- Primary button -->
    <button hlmBtn variant="primary">Save Event</button>

    <!-- Secondary button -->
    <button hlmBtn variant="secondary">Cancel</button>

    <!-- Destructive button -->
    <button hlmBtn variant="destructive">Delete Event</button>

    <!-- Ghost button (outline) -->
    <button hlmBtn variant="ghost">Learn More</button>

    <!-- Outline button -->
    <button hlmBtn variant="outline">Preview</button>

    <!-- Loading state -->
    <button hlmBtn [disabled]="isLoading()">
      @if (isLoading()) {
        <i class="fa-solid fa-spinner fa-spin mr-2"></i>
        Saving...
      } @else {
        <i class="fa-solid fa-save mr-2"></i>
        Save
      }
    </button>

    <!-- With icon -->
    <button hlmBtn variant="primary" class="w-full">
      <i class="fa-solid fa-plus mr-2"></i>
      Create Event
    </button>
  `,
})
export class ButtonDemoComponent {
  isLoading = signal(false);
}
```

### Button Sizes

```typescript
// ✅ CORRECT: Button size variations
<button hlmBtn size="sm" variant="primary">Small</button>
<button hlmBtn size="default" variant="primary">Default</button>
<button hlmBtn size="lg" variant="primary">Large</button>
<button hlmBtn size="icon" variant="primary">
  <i class="fa-solid fa-heart"></i>
</button>
```

---

## Card Component

### Basic Card

```typescript
// ✅ CORRECT: Card with Spartan-NG
@Component({
  selector: 'app-event-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div hlmCard class="p-6">
      <div class="flex items-start justify-between">
        <div>
          <h2 class="text-xl font-semibold">{{ event.name }}</h2>
          <p class="text-sm text-muted-foreground mt-1">{{ event.date }}</p>
        </div>
        <i class="fa-solid fa-bookmark text-primary"></i>
      </div>

      <div class="mt-4 space-y-2">
        <div class="flex items-center gap-2 text-sm">
          <i class="fa-solid fa-map-pin text-muted-foreground"></i>
          <span>{{ event.location }}</span>
        </div>
        <div class="flex items-center gap-2 text-sm">
          <i class="fa-solid fa-users text-muted-foreground"></i>
          <span>{{ event.attendees }} attending</span>
        </div>
      </div>

      <div class="mt-6 flex gap-3">
        <button hlmBtn variant="primary" class="flex-1">
          <i class="fa-solid fa-check mr-2"></i>
          RSVP
        </button>
        <button hlmBtn variant="outline" class="flex-1">
          <i class="fa-solid fa-share mr-2"></i>
          Share
        </button>
      </div>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }
    `,
  ],
})
export class EventCardComponent {
  @Input() event!: Event;
}
```

### Card Variants

```typescript
// ✅ CORRECT: Different card styles
<div hlmCard class="bg-slate-50 dark:bg-slate-900">
  Light background card
</div>

<div hlmCard class="border-2 border-primary">
  Emphasized card
</div>

<div hlmCard class="shadow-lg">
  Elevated card
</div>
```

---

## Alert Component

### Alert Variants

```typescript
// ✅ CORRECT: Alert with different variants
@Component({
  selector: 'app-alert-demo',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!-- Default alert -->
    <div hlmAlert>
      <i class="fa-solid fa-circle-info mr-2"></i>
      This is an informational alert
    </div>

    <!-- Success alert -->
    <div hlmAlert variant="success">
      <i class="fa-solid fa-circle-check mr-2"></i>
      Event created successfully
    </div>

    <!-- Warning alert -->
    <div hlmAlert variant="warning">
      <i class="fa-solid fa-triangle-exclamation mr-2"></i>
      Only 2 spots remaining for this event
    </div>

    <!-- Destructive alert -->
    <div hlmAlert variant="destructive">
      <i class="fa-solid fa-circle-exclamation mr-2"></i>
      Error: Failed to save event
    </div>
  `,
})
export class AlertDemoComponent {}
```

---

## Input Component

### Basic Input

```typescript
// ✅ CORRECT: Input with Tailwind styling
@Component({
  selector: 'app-input-demo',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <form [formGroup]="form">
      <!-- Text input -->
      <div class="space-y-2">
        <label class="text-sm font-medium">Event Name</label>
        <input
          hlmInput
          type="text"
          formControlName="eventName"
          placeholder="Enter event name"
          class="w-full"
        />
      </div>

      <!-- Email input -->
      <div class="space-y-2 mt-4">
        <label class="text-sm font-medium">Email</label>
        <input
          hlmInput
          type="email"
          formControlName="email"
          placeholder="your@email.com"
          class="w-full"
        />
      </div>

      <!-- Date input -->
      <div class="space-y-2 mt-4">
        <label class="text-sm font-medium">Event Date</label>
        <input hlmInput type="date" formControlName="date" class="w-full" />
      </div>

      <!-- Number input with icon -->
      <div class="space-y-2 mt-4">
        <label class="text-sm font-medium">Max Guests</label>
        <div class="relative">
          <i
            class="fa-solid fa-users absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground"
          ></i>
          <input
            hlmInput
            type="number"
            formControlName="maxGuests"
            placeholder="0"
            class="w-full pl-10"
          />
        </div>
      </div>

      <!-- Textarea -->
      <div class="space-y-2 mt-4">
        <label class="text-sm font-medium">Description</label>
        <textarea
          hlmInput
          formControlName="description"
          placeholder="Event description"
          rows="4"
          class="w-full"
        ></textarea>
      </div>
    </form>
  `,
})
export class InputDemoComponent {
  private fb = inject(FormBuilder);

  form = this.fb.group({
    eventName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    date: ['', Validators.required],
    maxGuests: [''],
    description: [''],
  });
}
```

### Input with Error State

```typescript
// ✅ CORRECT: Input validation feedback
<div class="space-y-2">
  <label class="text-sm font-medium">Event Name</label>
  <input
    hlmInput
    formControlName="eventName"
    [class.border-destructive]="
      form.get('eventName')?.invalid &&
      form.get('eventName')?.touched
    "
    class="w-full"
  />

  @if (form.get('eventName')?.hasError('required') &&
       form.get('eventName')?.touched) {
    <div class="text-sm text-destructive flex items-center gap-1">
      <i class="fa-solid fa-circle-exclamation"></i>
      Event name is required
    </div>
  }
</div>
```

---

## Select Component

### Basic Select

```typescript
// ✅ CORRECT: Select dropdown with Spartan-NG
@Component({
  selector: 'app-select-demo',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="space-y-2">
      <label class="text-sm font-medium">Event Type</label>
      <select hlmSelect formControlName="eventType" class="w-full">
        <option value="">Select an option</option>
        <option value="wedding">Wedding</option>
        <option value="conference">Conference</option>
        <option value="birthday">Birthday Party</option>
        <option value="corporate">Corporate Event</option>
      </select>
    </div>

    <!-- With icon -->
    <div class="space-y-2 mt-4">
      <label class="text-sm font-medium">Vendor Category</label>
      <div class="relative">
        <i
          class="fa-solid fa-list absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground pointer-events-none"
        ></i>
        <select hlmSelect formControlName="category" class="w-full pl-10">
          <option value="">Select category</option>
          <option value="photography">Photography</option>
          <option value="catering">Catering</option>
          <option value="venue">Venue</option>
          <option value="music">Music & Entertainment</option>
        </select>
      </div>
    </div>
  `,
})
export class SelectDemoComponent {
  private fb = inject(FormBuilder);

  form = this.fb.group({
    eventType: [''],
    category: [''],
  });
}
```

---

## Form Integration

### Complete Form with Validation

```typescript
// ✅ CORRECT: Full form with Spartan-NG components
@Component({
  selector: 'app-event-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div hlmCard class="p-6 max-w-2xl mx-auto">
      <h1 class="text-2xl font-bold mb-6">
        <i class="fa-solid fa-calendar-plus mr-2 text-primary"></i>
        Create Event
      </h1>

      <form [formGroup]="form" (ngSubmit)="onSubmit()">
        <!-- Event Name -->
        <div class="space-y-2 mb-6">
          <label class="text-sm font-medium">Event Name</label>
          <input
            hlmInput
            type="text"
            formControlName="name"
            placeholder="Give your event a name"
            class="w-full"
          />
          @if (form.get('name')?.hasError('required') && form.get('name')?.touched) {
            <div class="text-sm text-destructive flex items-center gap-1">
              <i class="fa-solid fa-circle-exclamation"></i>
              Event name is required
            </div>
          }
        </div>

        <!-- Date -->
        <div class="space-y-2 mb-6">
          <label class="text-sm font-medium">Date</label>
          <input hlmInput type="date" formControlName="date" class="w-full" />
        </div>

        <!-- Location Type -->
        <div class="space-y-2 mb-6">
          <label class="text-sm font-medium">Location Type</label>
          <select hlmSelect formControlName="locationType" class="w-full">
            <option value="physical">Physical Venue</option>
            <option value="virtual">Virtual/Online</option>
            <option value="hybrid">Hybrid</option>
          </select>
        </div>

        <!-- Location (conditional) -->
        @if (form.get('locationType')?.value !== 'virtual') {
          <div class="space-y-2 mb-6">
            <label class="text-sm font-medium">Venue Address</label>
            <input
              hlmInput
              type="text"
              formControlName="location"
              placeholder="Enter venue address"
              class="w-full"
            />
          </div>
        }

        <!-- Virtual URL (conditional) -->
        @if (form.get('locationType')?.value !== 'physical') {
          <div class="space-y-2 mb-6">
            <label class="text-sm font-medium">Meeting URL</label>
            <input
              hlmInput
              type="url"
              formControlName="virtualUrl"
              placeholder="https://..."
              class="w-full"
            />
          </div>
        }

        <!-- Description -->
        <div class="space-y-2 mb-6">
          <label class="text-sm font-medium">Description</label>
          <textarea
            hlmInput
            formControlName="description"
            placeholder="Tell us about your event"
            rows="4"
            class="w-full"
          ></textarea>
        </div>

        <!-- Max Capacity -->
        <div class="space-y-2 mb-6">
          <label class="text-sm font-medium">Max Capacity</label>
          <input
            hlmInput
            type="number"
            formControlName="maxCapacity"
            placeholder="Leave blank for unlimited"
            class="w-full"
          />
        </div>

        <!-- Error Message -->
        @if (submitError()) {
          <div hlmAlert variant="destructive" class="mb-6">
            <i class="fa-solid fa-circle-exclamation mr-2"></i>
            {{ submitError() }}
          </div>
        }

        <!-- Success Message -->
        @if (submitSuccess()) {
          <div hlmAlert variant="success" class="mb-6">
            <i class="fa-solid fa-circle-check mr-2"></i>
            Event created successfully!
          </div>
        }

        <!-- Submit Button -->
        <div class="flex gap-3">
          <button
            hlmBtn
            type="submit"
            variant="primary"
            [disabled]="form.invalid || isSubmitting()"
            class="flex-1"
          >
            @if (isSubmitting()) {
              <i class="fa-solid fa-spinner fa-spin mr-2"></i>
              Creating...
            } @else {
              <i class="fa-solid fa-check mr-2"></i>
              Create Event
            }
          </button>

          <button hlmBtn type="button" variant="outline" (click)="onCancel()" class="flex-1">
            <i class="fa-solid fa-xmark mr-2"></i>
            Cancel
          </button>
        </div>
      </form>
    </div>
  `,
})
export class EventFormComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private eventService = inject(EventService);

  form = this.fb.group({
    name: ['', Validators.required],
    date: ['', Validators.required],
    locationType: ['physical'],
    location: [''],
    virtualUrl: [''],
    description: [''],
    maxCapacity: [''],
  });

  isSubmitting = signal(false);
  submitError = signal<string | null>(null);
  submitSuccess = signal(false);

  async onSubmit() {
    if (!this.form.valid()) return;

    this.isSubmitting.set(true);
    this.submitError.set(null);
    this.submitSuccess.set(false);

    try {
      const event = await this.eventService.createEvent(this.form.getRawValue());
      this.submitSuccess.set(true);
      this.form.reset();

      setTimeout(() => {
        this.router.navigate(['/events', event.id]);
      }, 1500);
    } catch (error: any) {
      this.submitError.set(error?.message || 'Failed to create event');
    } finally {
      this.isSubmitting.set(false);
    }
  }

  onCancel() {
    this.router.navigate(['/events']);
  }
}
```

---

## Dialog/Modal Component

### Basic Dialog

```typescript
// ✅ CORRECT: Dialog with Spartan-NG
@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div hlmSheet [isOpen]="isOpen()" (isOpenChange)="onClose()">
      <div
        class="fixed inset-0 z-50 bg-black/50 data-[state=open]:animate-in data-[state=closed]:animate-out"
        [class.hidden]="!isOpen()"
      ></div>

      <div
        class="fixed left-[50%] top-[50%] z-50 w-full max-w-md translate-x-[-50%] translate-y-[-50%] bg-white dark:bg-slate-950 p-6 rounded-lg shadow-lg"
        [class.hidden]="!isOpen()"
      >
        <div class="flex items-start justify-between mb-4">
          <div>
            <h2 class="text-lg font-semibold flex items-center gap-2">
              <i class="fa-solid fa-triangle-exclamation text-warning"></i>
              Confirm Delete
            </h2>
            <p class="text-sm text-muted-foreground mt-1">This action cannot be undone</p>
          </div>
        </div>

        <p class="text-sm mb-6">Are you sure you want to delete "{{ itemName }}"?</p>

        <div class="flex gap-3">
          <button hlmBtn variant="destructive" (click)="onConfirm()" class="flex-1">
            <i class="fa-solid fa-trash mr-2"></i>
            Delete
          </button>
          <button hlmBtn variant="outline" (click)="onCancel()" class="flex-1">
            <i class="fa-solid fa-xmark mr-2"></i>
            Cancel
          </button>
        </div>
      </div>
    </div>
  `,
})
export class ConfirmDialogComponent {
  @Input() isOpen = signal(false);
  @Input() itemName = '';
  @Output() confirmed = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  onConfirm() {
    this.confirmed.emit();
    this.isOpen.set(false);
  }

  onCancel() {
    this.cancelled.emit();
    this.isOpen.set(false);
  }

  onClose() {
    if (!this.isOpen()) {
      this.cancelled.emit();
    }
  }
}
```

---

## Theme Integration (Light/Dark Mode)

### Theme Service with Spartan-NG

```typescript
// ✅ CORRECT: Dark mode support
@Injectable({ providedIn: 'root' })
export class ThemeService {
  isDark = signal(false);

  constructor() {
    // Check system preference
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    this.isDark.set(prefersDark);
    this.applyTheme(prefersDark);

    // Listen to system changes
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
      this.isDark.set(e.matches);
      this.applyTheme(e.matches);
    });
  }

  toggleTheme() {
    const newDark = !this.isDark();
    this.isDark.set(newDark);
    this.applyTheme(newDark);
  }

  private applyTheme(isDark: boolean) {
    if (isDark) {
      document.documentElement.classList.add('dark');
    } else {
      document.documentElement.classList.remove('dark');
    }
  }
}

// Usage in component
@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <nav class="bg-white dark:bg-slate-950 border-b">
      <div class="flex items-center justify-between p-4">
        <h1 class="text-2xl font-bold text-primary">Siora</h1>

        <button hlmBtn variant="ghost" size="icon" (click)="theme.toggleTheme()">
          @if (theme.isDark()) {
            <i class="fa-solid fa-sun"></i>
          } @else {
            <i class="fa-solid fa-moon"></i>
          }
        </button>
      </div>
    </nav>
  `,
})
export class NavbarComponent {
  theme = inject(ThemeService);
}
```

---

## Platform-Specific Styling

### Platform Detection with Spartan-NG

```typescript
// ✅ CORRECT: Platform-aware styling
@Component({
  selector: 'app-event-detail',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div
      [class.ios]="platform.isIOS()"
      [class.android]="platform.isAndroid()"
      [class.web]="platform.isWeb()"
    >
      <!-- Safe area handling for notch on iOS -->
      <div class="header" *ngIf="platform.isIOS()">
        <i class="fa-solid fa-arrow-left"></i>
        Back
      </div>

      <!-- Material Design buttons on Android -->
      <button hlmBtn [class]="platform.isAndroid() ? 'uppercase text-xs font-bold' : ''">
        Save Event
      </button>

      <!-- Web-specific sidebar -->
      @if (platform.isWeb()) {
        <aside class="w-64 border-r">
          <!-- Desktop navigation -->
        </aside>
      }

      <!-- Mobile menu -->
      @if (platform.isNative()) {
        <nav class="bottom-nav fixed bottom-0 w-full bg-white dark:bg-slate-950 border-t">
          <!-- Mobile navigation -->
        </nav>
      }
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      /* iOS safe area (notch) */
      :host.ios .header {
        padding-top: env(safe-area-inset-top);
      }

      :host.ios .bottom-nav {
        padding-bottom: env(safe-area-inset-bottom);
      }

      /* Android Material Design */
      :host.android button::ng-deep {
        border-radius: 4px;
      }

      /* Web sidebar */
      :host.web {
        display: flex;
      }
    `,
  ],
})
export class EventDetailComponent {
  platform = inject(PlatformService);
}
```

---

## Dropdown/Menu Component

### Dropdown Menu

```typescript
// ✅ CORRECT: Dropdown with Spartan-NG
@Component({
  selector: 'app-dropdown-demo',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="relative">
      <button hlmBtn variant="outline" (click)="toggleMenu()">
        <i class="fa-solid fa-ellipsis-vertical"></i>
        Options
      </button>

      @if (isMenuOpen()) {
        <div
          class="absolute right-0 mt-2 w-48 bg-white dark:bg-slate-950 rounded-md shadow-lg border z-50"
        >
          <button
            class="w-full text-left px-4 py-2 hover:bg-slate-100 dark:hover:bg-slate-800 flex items-center gap-2"
            (click)="onEdit()"
          >
            <i class="fa-solid fa-edit"></i>
            Edit
          </button>

          <button
            class="w-full text-left px-4 py-2 hover:bg-slate-100 dark:hover:bg-slate-800 flex items-center gap-2"
            (click)="onDuplicate()"
          >
            <i class="fa-solid fa-copy"></i>
            Duplicate
          </button>

          <div class="border-t my-1"></div>

          <button
            class="w-full text-left px-4 py-2 hover:bg-slate-100 dark:hover:bg-slate-800 flex items-center gap-2 text-destructive"
            (click)="onDelete()"
          >
            <i class="fa-solid fa-trash"></i>
            Delete
          </button>
        </div>
      }
    </div>
  `,
})
export class DropdownDemoComponent {
  isMenuOpen = signal(false);

  toggleMenu() {
    this.isMenuOpen.update((open) => !open);
  }

  onEdit() {
    console.log('Edit clicked');
    this.isMenuOpen.set(false);
  }

  onDuplicate() {
    console.log('Duplicate clicked');
    this.isMenuOpen.set(false);
  }

  onDelete() {
    console.log('Delete clicked');
    this.isMenuOpen.set(false);
  }
}
```

---

## Badge/Tag Component

### Badge Display

```typescript
// ✅ CORRECT: Badges with Spartan-NG
@Component({
  selector: 'app-badge-demo',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!-- Default badge -->
    <span
      class="inline-flex items-center gap-2 px-2.5 py-0.5 rounded-full text-xs font-medium bg-slate-100 dark:bg-slate-800 text-slate-900 dark:text-slate-100"
    >
      <i class="fa-solid fa-tag"></i>
      Wedding
    </span>

    <!-- Success badge -->
    <span
      class="inline-flex items-center gap-2 px-2.5 py-0.5 rounded-full text-xs font-medium bg-green-100 dark:bg-green-900 text-green-800 dark:text-green-200"
    >
      <i class="fa-solid fa-circle-check"></i>
      Confirmed
    </span>

    <!-- Warning badge -->
    <span
      class="inline-flex items-center gap-2 px-2.5 py-0.5 rounded-full text-xs font-medium bg-yellow-100 dark:bg-yellow-900 text-yellow-800 dark:text-yellow-200"
    >
      <i class="fa-solid fa-exclamation"></i>
      Pending
    </span>

    <!-- Destructive badge -->
    <span
      class="inline-flex items-center gap-2 px-2.5 py-0.5 rounded-full text-xs font-medium bg-red-100 dark:bg-red-900 text-red-800 dark:text-red-200"
    >
      <i class="fa-solid fa-circle-xmark"></i>
      Cancelled
    </span>
  `,
})
export class BadgeDemoComponent {}
```

---

## Siora Feature Examples

### Event List with Spartan-NG

```typescript
// ✅ CORRECT: Real event list implementation
@Component({
  selector: 'app-event-list',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-4">
      @if (isLoading()) {
        <div class="flex items-center justify-center py-8">
          <i class="fa-solid fa-spinner fa-spin text-2xl text-primary"></i>
        </div>
      } @else if (hasError()) {
        <div hlmAlert variant="destructive">
          <i class="fa-solid fa-circle-exclamation mr-2"></i>
          {{ error() }}
        </div>
      } @else if (events().length === 0) {
        <div class="text-center py-8">
          <i class="fa-solid fa-calendar text-4xl text-muted-foreground mb-4"></i>
          <p class="text-muted-foreground">No events found</p>
        </div>
      } @else {
        @for (event of events(); track event.id) {
          <div hlmCard class="p-4 hover:shadow-md transition-shadow">
            <div class="flex items-start gap-4">
              <div class="flex-1">
                <h3 class="font-semibold text-lg">{{ event.name }}</h3>
                <div class="flex flex-wrap gap-3 mt-3 text-sm text-muted-foreground">
                  <span class="flex items-center gap-1">
                    <i class="fa-solid fa-calendar-days"></i>
                    {{ event.date | date: 'short' }}
                  </span>
                  <span class="flex items-center gap-1">
                    <i class="fa-solid fa-map-pin"></i>
                    {{ event.location }}
                  </span>
                  <span class="flex items-center gap-1">
                    <i class="fa-solid fa-users"></i>
                    {{ event.attendeeCount }} attending
                  </span>
                </div>
              </div>

              <span [class]="getBadgeClass(event.status)">
                {{ event.status }}
              </span>
            </div>

            <div class="mt-4 flex gap-2">
              <button hlmBtn variant="primary" size="sm" (click)="viewEvent(event.id)">
                <i class="fa-solid fa-arrow-right"></i>
                View
              </button>
              <button hlmBtn variant="outline" size="sm" (click)="editEvent(event.id)">
                <i class="fa-solid fa-edit"></i>
                Edit
              </button>
            </div>
          </div>
        }
      }
    </div>
  `,
})
export class EventListComponent {
  private eventService = inject(EventService);
  private router = inject(Router);

  eventResource = resource({
    loader: () => this.eventService.getEvents(),
  });

  events = computed(() => this.eventResource.value() ?? []);
  isLoading = computed(() => this.eventResource.isLoading());
  hasError = computed(() => this.eventResource.hasError());
  error = computed(() => this.eventResource.error()?.message ?? '');

  getBadgeClass(status: string): string {
    const baseClass =
      'inline-flex items-center gap-2 px-2.5 py-0.5 rounded-full text-xs font-medium';
    const statusClass =
      {
        draft: 'bg-slate-100 text-slate-900 dark:bg-slate-800 dark:text-slate-100',
        published: 'bg-blue-100 text-blue-900 dark:bg-blue-900 dark:text-blue-200',
        active: 'bg-green-100 text-green-900 dark:bg-green-900 dark:text-green-200',
        completed: 'bg-gray-100 text-gray-900 dark:bg-gray-800 dark:text-gray-200',
        cancelled: 'bg-red-100 text-red-900 dark:bg-red-900 dark:text-red-200',
      }[status] || 'bg-slate-100 text-slate-900 dark:bg-slate-800 dark:text-slate-100';

    return `${baseClass} ${statusClass}`;
  }

  viewEvent(id: number) {
    this.router.navigate(['/events', id]);
  }

  editEvent(id: number) {
    this.router.navigate(['/events', id, 'edit']);
  }
}
```

---

## DO's and DON'Ts

### DO ✅

- Use Spartan-NG directives for all UI components (`hlmBtn`, `hlmCard`, `hlmAlert`, `hlmInput`, etc.)
- Style with Tailwind CSS utilities for layout, spacing, responsive design
- Use Font Awesome Pro icons for rich iconography
- Combine Spartan directives with Tailwind classes
- Use platform-specific styling with `.ios`, `.android`, `.web` classes
- Test on all platforms (web, iOS, Android)
- Use `dark:` Tailwind prefix for dark mode support
- Use signals for component state with Spartan components
- Keep components small and focused

### DON'T ❌

- Never mix component libraries (WebAwesome + Spartan-NG)
- Never style Spartan components with custom CSS when Tailwind can do it
- Never forget Font Awesome Pro icons in the template
- Never skip platform-specific testing
- Never use inline styles when Tailwind utilities exist
- Never create new button/input/card styles - use Spartan variants
- Never forget accessibility attributes
- Never hardcode platform checks without service injection

---

## Migration from WebAwesome

If migrating from WebAwesome:

1. **Remove WebAwesome imports**: `import { WebAwesomeModule }`
2. **Replace WebAwesome elements** with Spartan-NG directives:
   - `<sl-button>` → `<button hlmBtn>`
   - `<sl-card>` → `<div hlmCard>`
   - `<sl-alert>` → `<div hlmAlert>`
   - `<sl-input>` → `<input hlmInput>`
   - `<sl-select>` → `<select hlmSelect>`
3. **Replace WebAwesome styles** with Tailwind CSS
4. **Keep Font Awesome icons** exactly as-is
5. **Test on all platforms** (web, iOS, Android)
6. **Use Spartan variants** instead of className manipulation

The result is cleaner, more maintainable code with better performance and platform consistency!
