---
name: forms
description: |
  Master Angular Signal Forms (reactive forms with signal state) for Siora projects. Provides patterns for form validation, dynamic fields, conditional validation, and async validators. Use this skill when: (1) Building reactive forms with FormBuilder; (2) Adding custom validators; (3) Creating dynamic form arrays; (4) Implementing async validation; (5) Combining forms with signals for UI state; (6) Testing form validation and submission
---

# Siora Angular Signal Forms Patterns

Reactive forms combined with signals for powerful form handling in Siora.

## Quick Start: Basic Form

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
    description: [''],
  });

  async onSubmit() {
    if (!this.form.valid()) return;

    this.submitting.set(true);
    try {
      const formValue = this.form.getRawValue();
      await this.eventService.createEvent(formValue);
      this.form.reset();
    } finally {
      this.submitting.set(false);
    }
  }
}
```

## Custom Validators

```typescript
// ✅ CORRECT: Custom validators
function dateInFutureValidator(control: AbstractControl): ValidationErrors | null {
  if (!control.value) return null;
  const selectedDate = new Date(control.value);
  const today = new Date();
  return selectedDate > today ? null : { dateInPast: true };
}

form = this.fb.group({
  name: ['', [Validators.required, Validators.minLength(3)]],
  date: ['', [Validators.required, dateInFutureValidator]],
});
```

## Dynamic Form Arrays

```typescript
// ✅ CORRECT: FormArray for dynamic fields
form = this.fb.group({
  eventName: ['', Validators.required],
  vendors: this.fb.array([])
});

get vendors(): FormArray {
  return this.form.get('vendors') as FormArray;
}

addVendor() {
  this.vendors.push(
    this.fb.group({
      name: ['', Validators.required]
    })
  );
}

removeVendor(index: number) {
  this.vendors.removeAt(index);
}
```

## Async Validators

```typescript
// ✅ CORRECT: Async validators (Promise-based)
function checkEmailUnique(eventService: EventService): AsyncValidatorFn {
  return (control: AbstractControl): Promise<ValidationErrors | null> => {
    if (!control.value) return Promise.resolve(null);

    return eventService
      .checkEmailExists(control.value)
      .then((exists) => (exists ? { emailTaken: true } : null))
      .catch(() => null);
  };
}

form = this.fb.group({
  email: ['', [Validators.required, Validators.email], [checkEmailUnique(this.eventService)]],
});
```

## Form with Signal State

```typescript
// ✅ CORRECT: Combine form with signal state
submitError = signal<string | null>(null);
submitSuccess = signal(false);
isSubmitting = signal(false);

async onSubmit() {
  if (!this.form.valid()) return;

  this.isSubmitting.set(true);
  this.submitError.set(null);
  this.submitSuccess.set(false);

  try {
    await this.eventService.createEvent(this.form.getRawValue());
    this.submitSuccess.set(true);
    this.form.reset();
  } catch (error: any) {
    this.submitError.set(error?.message || 'Failed to create event');
  } finally {
    this.isSubmitting.set(false);
  }
}
```

## Complete Reference

See [forms-patterns.md](references/forms-patterns.md) for detailed patterns including:

- Conditional field validation
- FormArray examples with track
- Testing Signal Forms
- Error message helpers
- DO's and DON'Ts checklist

## Key Principles

✅ **DO**

- Use FormBuilder for all forms
- Combine forms with signals for UI state
- Validate on submit, not per keystroke
- Use async validators for server checks
- Test form validity and submissions

❌ **DON'T**

- Never use ngModel with reactive forms
- Never ignore form validation
- Never submit without checking form.valid()
- Never forget to handle async submission states
