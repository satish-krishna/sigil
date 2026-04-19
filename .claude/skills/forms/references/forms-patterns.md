# Angular Signal Forms Patterns

Complete patterns for Angular v21 Signal Forms (reactive forms with signals).

## Basic Form

```typescript
// ✅ CORRECT: Signal Forms with FormBuilder
@Component({
  selector: 'app-event-form',
  standalone: true,
  imports: [ReactiveFormsModule, WebAwesomeModule],
  template: `
    <form [formGroup]="form" (ngSubmit)="onSubmit()">
      <sl-input
        formControlName="name"
        placeholder="Event name"
        [class.error]="form.get('name')?.invalid && form.get('name')?.touched"
      ></sl-input>

      @if (form.get('name')?.errors?.['required']) {
        <p class="error"><i class="fa-solid fa-circle-exclamation"></i> Name is required</p>
      }

      <sl-input
        type="date"
        formControlName="date"
        [class.error]="form.get('date')?.invalid && form.get('date')?.touched"
      ></sl-input>

      @if (form.get('date')?.errors?.['required']) {
        <p class="error"><i class="fa-solid fa-circle-exclamation"></i> Date is required</p>
      }

      <sl-button type="submit" [disabled]="!form.valid() || submitting()">
        @if (submitting()) {
          <i class="fa-solid fa-spinner fa-spin"></i> Saving...
        } @else {
          <i class="fa-solid fa-save"></i> Save Event
        }
      </sl-button>
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
  imports: [ReactiveFormsModule, CommonModule, WebAwesomeModule],
  template: `
    <form [formGroup]="form" (ngSubmit)="onSubmit()">
      <sl-input formControlName="eventName" placeholder="Event name"></sl-input>

      <div formArrayName="vendors">
        <h3>Add Vendors</h3>
        @for (vendor of vendors.controls; let i = $index; track i) {
          <div [formGroupName]="i">
            <sl-input formControlName="name" placeholder="Vendor name"></sl-input>
            <sl-button type="button" (click)="removeVendor(i)">
              <i class="fa-solid fa-trash"></i> Remove
            </sl-button>
          </div>
        }
        <sl-button type="button" (click)="addVendor()">
          <i class="fa-solid fa-plus"></i> Add Vendor
        </sl-button>
      </div>

      <sl-button type="submit" [disabled]="!form.valid()">Submit</sl-button>
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
  template: `
    <form [formGroup]="form">
      <sl-select formControlName="eventType">
        <sl-option value="online">Online</sl-option>
        <sl-option value="inperson">In-Person</sl-option>
        <sl-option value="hybrid">Hybrid</sl-option>
      </sl-select>

      @if (
        form.get('eventType')?.value === 'inperson' || form.get('eventType')?.value === 'hybrid'
      ) {
        <sl-input formControlName="location" placeholder="Location" required></sl-input>
      }

      @if (form.get('eventType')?.value === 'online' || form.get('eventType')?.value === 'hybrid') {
        <sl-input formControlName="meetingUrl" placeholder="Meeting URL" required></sl-input>
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
    <form [formGroup]="form" (ngSubmit)="onSubmit()">
      <sl-input formControlName="name" placeholder="Event name"></sl-input>

      @if (submitError()) {
        <sl-alert type="error">{{ submitError() }}</sl-alert>
      }

      @if (submitSuccess()) {
        <sl-alert type="success">Event created successfully!</sl-alert>
      }

      <sl-button type="submit" [disabled]="isSubmitting()">
        @if (isSubmitting()) {
          Saving...
        } @else {
          Save
        }
      </sl-button>
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
