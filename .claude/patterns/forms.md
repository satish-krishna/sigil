# Angular Signal Forms Patterns

Complete patterns for Angular v21 Signal Forms (reactive forms with signals).

## Basic Form

```typescript
// ✅ CORRECT: Signal Forms with FormBuilder and Spartan-NG
@Component({
  selector: 'app-event-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <form [formGroup]="form" (ngSubmit)="onSubmit()">
      <div class="space-y-6">
        <!-- Event Name -->
        <div class="space-y-2">
          <label class="text-sm font-medium">Event Name</label>
          <input
            hlmInput
            type="text"
            formControlName="name"
            placeholder="Event name"
            [class.border-destructive]="form.get('name')?.invalid && form.get('name')?.touched"
            class="w-full"
          />

          @if (form.get('name')?.errors?.['required'] && form.get('name')?.touched) {
            <div class="text-sm text-destructive flex items-center gap-1">
              <i class="fa-solid fa-circle-exclamation"></i>
              Name is required
            </div>
          }
        </div>

        <!-- Date -->
        <div class="space-y-2">
          <label class="text-sm font-medium">Date</label>
          <input
            hlmInput
            type="date"
            formControlName="date"
            [class.border-destructive]="form.get('date')?.invalid && form.get('date')?.touched"
            class="w-full"
          />

          @if (form.get('date')?.errors?.['required'] && form.get('date')?.touched) {
            <div class="text-sm text-destructive flex items-center gap-1">
              <i class="fa-solid fa-circle-exclamation"></i>
              Date is required
            </div>
          }
        </div>

        <!-- Submit Button -->
        <button
          hlmBtn
          type="submit"
          variant="primary"
          [disabled]="!form.valid() || submitting()"
          class="w-full"
        >
          @if (submitting()) {
            <i class="fa-solid fa-spinner fa-spin mr-2"></i>
            Saving...
          } @else {
            <i class="fa-solid fa-save mr-2"></i>
            Save Event
          }
        </button>
      </div>
    </form>
  `,
})
export class EventFormComponent {
  private fb = inject(FormBuilder);
  private eventService = inject(EventService);

  submitting = signal(false);

  form = this.fb.group({
    name: ['', Validators.required],
    date: ['', Validators.required],
    description: [''],
  });

  async onSubmit() {
    if (!this.form.valid()) return;

    this.submitting.set(true);
    try {
      const formValue = this.form.getRawValue();
      await this.eventService.createEvent(formValue);
      // Reset form after success
      this.form.reset();
    } finally {
      this.submitting.set(false);
    }
  }
}

// ❌ WRONG: Old Reactive Forms without signals
// form = new FormGroup({
//   name: new FormControl('')
// });
```

## Form with Custom Validators

```typescript
// ✅ CORRECT: Custom validators
function dateInFutureValidator(control: AbstractControl): ValidationErrors | null {
  if (!control.value) return null;

  const selectedDate = new Date(control.value);
  const today = new Date();
  today.setHours(0, 0, 0, 0);

  return selectedDate > today ? null : { dateInPast: true };
}

@Component({...})
export class EventFormComponent {
  private fb = inject(FormBuilder);

  form = this.fb.group({
    name: ['', [Validators.required, Validators.minLength(3)]],
    date: ['', [Validators.required, dateInFutureValidator]],
    maxAttendees: ['', [Validators.required, Validators.min(1)]]
  });

  getNameError(): string {
    const control = this.form.get('name');
    if (control?.hasError('required')) return 'Name is required';
    if (control?.hasError('minlength')) return 'Name must be at least 3 characters';
    return '';
  }
}
```

## Form with Dynamic Fields

```typescript
// ✅ CORRECT: FormArray for dynamic fields
@Component({
  selector: 'app-event-form',
  standalone: true,
  imports: [ReactiveFormsModule, CommonModule],
  template: `
    <form [formGroup]="form" (ngSubmit)="onSubmit()" class="space-y-6">
      <div class="space-y-2">
        <label class="text-sm font-medium">Event Name</label>
        <input hlmInput formControlName="eventName" placeholder="Event name" class="w-full" />
      </div>

      <div formArrayName="vendors" class="space-y-4">
        <div class="flex items-center justify-between">
          <h3 class="font-semibold">Add Vendors</h3>
          <span class="text-xs text-muted-foreground">{{ vendors.length }} added</span>
        </div>

        @for (vendor of vendors.controls; let i = $index; track i) {
          <div [formGroupName]="i" hlmCard class="p-4">
            <div class="flex gap-3 items-end">
              <div class="flex-1">
                <label class="text-xs font-medium text-muted-foreground">Vendor Name</label>
                <input
                  hlmInput
                  formControlName="name"
                  placeholder="Vendor name"
                  class="w-full mt-1"
                />
              </div>
              <button
                type="button"
                hlmBtn
                variant="destructive"
                size="sm"
                (click)="removeVendor(i)"
              >
                <i class="fa-solid fa-trash"></i>
              </button>
            </div>
          </div>
        }

        <button type="button" hlmBtn variant="outline" class="w-full" (click)="addVendor()">
          <i class="fa-solid fa-plus mr-2"></i>
          Add Vendor
        </button>
      </div>

      <button hlmBtn type="submit" variant="primary" [disabled]="!form.valid()" class="w-full">
        Submit
      </button>
    </form>
  `,
})
export class EventFormComponent {
  private fb = inject(FormBuilder);

  form = this.fb.group({
    eventName: ['', Validators.required],
    vendors: this.fb.array([]),
  });

  get vendors(): FormArray {
    return this.form.get('vendors') as FormArray;
  }

  addVendor() {
    this.vendors.push(
      this.fb.group({
        name: ['', Validators.required],
        category: [''],
      })
    );
  }

  removeVendor(index: number) {
    this.vendors.removeAt(index);
  }

  onSubmit() {
    if (this.form.valid) {
      console.log(this.form.getRawValue());
    }
  }
}
```

## Form with Conditional Fields

```typescript
// ✅ CORRECT: Conditional validation
@Component({
  selector: 'app-event-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <form [formGroup]="form" class="space-y-4">
      <div class="space-y-2">
        <label class="text-sm font-medium">Event Type</label>
        <select hlmSelect formControlName="eventType" class="w-full">
          <option value="online">Online</option>
          <option value="inperson">In-Person</option>
          <option value="hybrid">Hybrid</option>
        </select>
      </div>

      @if (
        form.get('eventType')?.value === 'inperson' || form.get('eventType')?.value === 'hybrid'
      ) {
        <div class="space-y-2">
          <label class="text-sm font-medium">Location</label>
          <input
            hlmInput
            formControlName="location"
            placeholder="Location"
            required
            class="w-full"
          />
        </div>
      }

      @if (form.get('eventType')?.value === 'online' || form.get('eventType')?.value === 'hybrid') {
        <div class="space-y-2">
          <label class="text-sm font-medium">Meeting URL</label>
          <input
            hlmInput
            type="url"
            formControlName="meetingUrl"
            placeholder="Meeting URL"
            required
            class="w-full"
          />
        </div>
      }
    </form>
  `,
})
export class EventFormComponent {
  private fb = inject(FormBuilder);

  form = this.fb.group({
    eventType: ['online'],
    location: [''],
    meetingUrl: [''],
  });

  constructor() {
    // Update validators when event type changes
    this.form.get('eventType')?.valueChanges.subscribe((type) => {
      const locationControl = this.form.get('location');
      const urlControl = this.form.get('meetingUrl');

      if (type === 'inperson' || type === 'hybrid') {
        locationControl?.setValidators(Validators.required);
      } else {
        locationControl?.clearValidators();
      }

      if (type === 'online' || type === 'hybrid') {
        urlControl?.setValidators(Validators.required);
      } else {
        urlControl?.clearValidators();
      }

      locationControl?.updateValueAndValidity();
      urlControl?.updateValueAndValidity();
    });
  }
}
```

## Form with Async Validation

```typescript
// ✅ CORRECT: Async validators (Promise-based)
function checkEmailUnique(eventService: EventService):
  AsyncValidatorFn {
  return (control: AbstractControl): Promise<ValidationErrors | null> => {
    if (!control.value) {
      return Promise.resolve(null);
    }

    return eventService.checkEmailExists(control.value)
      .then(exists => exists ? { emailTaken: true } : null)
      .catch(() => null);
  };
}

@Component({...})
export class EventFormComponent {
  private fb = inject(FormBuilder);
  private eventService = inject(EventService);

  form = this.fb.group({
    email: [
      '',
      [Validators.required, Validators.email],
      [checkEmailUnique(this.eventService)]
    ]
  });
}
```

## Form with State Signal

```typescript
// ✅ CORRECT: Combine form with signal state
@Component({
  selector: 'app-event-form',
  standalone: true,
  template: `
    <form [formGroup]="form" (ngSubmit)="onSubmit()" class="space-y-4">
      <div class="space-y-2">
        <label class="text-sm font-medium">Event Name</label>
        <input hlmInput formControlName="name" placeholder="Event name" class="w-full" />
      </div>

      @if (submitError()) {
        <div hlmAlert variant="destructive">
          <i class="fa-solid fa-circle-exclamation mr-2"></i>
          {{ submitError() }}
        </div>
      }

      @if (submitSuccess()) {
        <div hlmAlert variant="success">
          <i class="fa-solid fa-circle-check mr-2"></i>
          Event created successfully!
        </div>
      }

      <button hlmBtn type="submit" variant="primary" [disabled]="isSubmitting()" class="w-full">
        @if (isSubmitting()) {
          <i class="fa-solid fa-spinner fa-spin mr-2"></i>
          Saving...
        } @else {
          <i class="fa-solid fa-save mr-2"></i>
          Save
        }
      </button>
    </form>
  `,
})
export class EventFormComponent {
  private fb = inject(FormBuilder);
  private eventService = inject(EventService);

  form = this.fb.group({
    name: ['', Validators.required],
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
      await this.eventService.createEvent(this.form.getRawValue());
      this.submitSuccess.set(true);
      this.form.reset();

      // Clear success message after 3 seconds
      setTimeout(() => this.submitSuccess.set(false), 3000);
    } catch (error: any) {
      this.submitError.set(error?.message || 'Failed to create event');
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
```

## Testing Signal Forms

```typescript
describe('EventForm', () => {
  let component: EventFormComponent;
  let fixture: ComponentFixture<EventFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EventFormComponent, ReactiveFormsModule],
    }).compileComponents();

    fixture = TestBed.createComponent(EventFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create form with validators', () => {
    const form = component.form;
    expect(form.invalid).toBe(true);

    form.patchValue({ name: 'Event 1', date: '2026-06-01' });
    expect(form.valid).toBe(true);
  });

  it('should show error on invalid name', () => {
    const nameControl = component.form.get('name');
    nameControl?.setValue('');
    nameControl?.markAsTouched();

    expect(nameControl?.hasError('required')).toBe(true);
  });

  it('should handle form submission', async () => {
    component.form.patchValue({
      name: 'New Event',
      date: '2026-06-01',
    });

    await component.onSubmit();
    expect(component.isSubmitting()).toBe(false);
  });
});
```

## DO's and DON'Ts

### DO ✅

- Use FormBuilder for all forms
- Combine forms with signals for UI state
- Use type guards in templates
- Validate on submit, not on every change
- Use async validators for server checks
- Test form validity and submissions

### DON'T ❌

- Never use ngModel with reactive forms
- Never ignore form validation
- Never submit without checking form.valid()
- Never forget to handle async submission states
- Never leave console.log() in production code
