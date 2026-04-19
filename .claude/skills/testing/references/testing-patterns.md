# Testing Patterns (Signals + Resource API)

Complete patterns for testing Angular v21 with Signals and Resource API.

## Testing Signals

```typescript
// ✅ CORRECT: Testing signals directly
describe('EventStore', () => {
  let store: EventStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [EventStore],
    });
    store = TestBed.inject(EventStore);
  });

  it('should initialize with empty events', () => {
    // Call signal as a function to get value
    expect(store.events().length).toBe(0);
  });

  it('should set events signal', () => {
    const mockEvents = [{ id: 1, name: 'Event 1' }];
    store.setEvents(mockEvents);

    // Check signal value
    expect(store.events()).toEqual(mockEvents);
    expect(store.events().length).toBe(1);
  });

  it('should update signal with update method', () => {
    store.setEvents([{ id: 1, name: 'Event 1' }]);
    store.addEvent({ id: 2, name: 'Event 2' });

    expect(store.events().length).toBe(2);
  });

  it('should compute derived signal', () => {
    store.setEvents([
      { id: 1, name: 'Event 1', cancelled: false },
      { id: 2, name: 'Event 2', cancelled: true },
    ]);

    // Computed signal updates automatically
    expect(store.filteredEvents().length).toBe(1);
    expect(store.filteredEvents()[0].name).toBe('Event 1');
  });

  it('should select event', () => {
    store.setEvents([{ id: 1, name: 'Event 1' }]);
    store.selectEvent(1);

    // Computed signal based on selectedEventId
    expect(store.selectedEvent()?.id).toBe(1);
  });
});
```

## Testing Resource API

```typescript
// ✅ CORRECT: Testing Resource API with HttpClientTestingModule
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
    // Trigger resource load
    service.eventsResource.reload();

    // Check initial loading state
    expect(service.eventsResource.isLoading()).toBe(true);

    // Mock HTTP response
    const req = httpMock.expectOne('/api/events');
    req.flush([
      { id: 1, name: 'Event 1' },
      { id: 2, name: 'Event 2' },
    ]);

    // Check final state
    expect(service.eventsResource.hasValue()).toBe(true);
    expect(service.eventsResource.value().length).toBe(2);
  });

  it('should handle resource errors', () => {
    service.eventsResource.reload();

    const req = httpMock.expectOne('/api/events');
    req.error(new ErrorEvent('Network error'));

    // Check error state
    expect(service.eventsResource.hasError()).toBe(true);
    expect(service.eventsResource.error()).toBeDefined();
  });

  it('should use default value on error', () => {
    service.eventsResource.reload();

    const req = httpMock.expectOne('/api/events');
    req.error(new ErrorEvent('Network error'));

    // Default value should still be available
    expect(service.eventsResource.value()).toEqual([]);
  });

  it('should refetch when params change', () => {
    // First fetch
    service.eventsByVendorResource.reload();
    let req = httpMock.expectOne('/api/vendors/1/events');
    req.flush([{ id: 1, name: 'Event 1' }]);

    // Change vendor parameter
    service.selectedVendorId.set(2);

    // Should fetch new data
    req = httpMock.expectOne('/api/vendors/2/events');
    req.flush([{ id: 2, name: 'Event 2' }]);

    expect(service.eventsByVendorResource.value()[0].id).toBe(2);
  });

  afterEach(() => {
    httpMock.verify();
  });
});
```

## Testing Components with Signals

```typescript
// ✅ CORRECT: Testing components with signals
describe('EventListComponent', () => {
  let component: EventListComponent;
  let fixture: ComponentFixture<EventListComponent>;
  let store: EventStore;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EventListComponent],
      providers: [EventStore],
    }).compileComponents();

    fixture = TestBed.createComponent(EventListComponent);
    component = fixture.componentInstance;
    store = TestBed.inject(EventStore);
    fixture.detectChanges();
  });

  it('should render event list from store', () => {
    store.setEvents([
      { id: 1, name: 'Event 1' },
      { id: 2, name: 'Event 2' },
    ]);

    fixture.detectChanges();

    const listItems = fixture.nativeElement.querySelectorAll('.event-item');
    expect(listItems.length).toBe(2);
  });

  it('should show loading state', () => {
    store.loading.set(true);
    fixture.detectChanges();

    const loadingText = fixture.nativeElement.textContent;
    expect(loadingText).toContain('Loading');
  });

  it('should show error state', () => {
    store.error.set('Failed to load events');
    fixture.detectChanges();

    const errorText = fixture.nativeElement.textContent;
    expect(errorText).toContain('Failed to load events');
  });

  it('should update view when signals change', () => {
    // Initial state
    expect(store.events().length).toBe(0);

    // Update signal
    store.setEvents([{ id: 1, name: 'Event 1' }]);
    fixture.detectChanges();

    // Component should reflect change
    const listItems = fixture.nativeElement.querySelectorAll('.event-item');
    expect(listItems.length).toBe(1);
  });
});
```

## Testing Forms

```typescript
// ✅ CORRECT: Testing Signal Forms
describe('EventFormComponent', () => {
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
    // Form starts invalid
    expect(component.form.invalid).toBe(true);

    // Fill required fields
    component.form.patchValue({
      name: 'New Event',
      date: '2026-06-01',
    });

    // Form becomes valid
    expect(component.form.valid).toBe(true);
  });

  it('should show validation errors', () => {
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

    // Check submission state
    expect(component.isSubmitting()).toBe(false);
  });

  it('should clear errors after successful submission', async () => {
    component.submitError.set('Some error');

    component.form.patchValue({
      name: 'New Event',
      date: '2026-06-01',
    });

    await component.onSubmit();

    // Error should be cleared
    expect(component.submitError()).toBeNull();
  });
});
```

## Testing Services with Promises

```typescript
// ✅ CORRECT: Testing async services with Promises
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

  it('should create event successfully', async () => {
    const mockEvent = { id: 1, name: 'Event 1' };

    const promise = service.createEvent({ name: 'Event 1' });

    const req = httpMock.expectOne('/api/events');
    req.flush(mockEvent);

    const result = await promise;
    expect(result.id).toBe(1);
  });

  it('should handle async errors', async () => {
    try {
      const promise = service.createEvent({ name: 'Event 1' });

      const req = httpMock.expectOne('/api/events');
      req.error(new ErrorEvent('Server error'));

      await promise;
      fail('Should have thrown error');
    } catch (error) {
      expect(error).toBeDefined();
    }
  });
});
```

## Testing Capacitor Features

```typescript
// ✅ CORRECT: Testing Capacitor with mocks
describe('PhotoService', () => {
  let service: PhotoService;
  let platform: PlatformService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PhotoService, PlatformService],
    });

    service = TestBed.inject(PhotoService);
    platform = TestBed.inject(PlatformService);
  });

  it('should use web fallback on web platform', async () => {
    // Mock web platform
    platform.isNative.set(false);
    platform.supportsCamera.set(false);

    await service.selectFromGallery();

    expect(service.photoUrl()).not.toBeNull();
  });

  it('should use native camera on native platform', async () => {
    // Mock native platform
    platform.isNative.set(true);

    // Mock Camera plugin
    spyOn(Camera, 'getPhoto').and.returnValue(Promise.resolve({ webPath: '/path/to/photo' }));

    await service.takePicture();

    expect(service.photoUrl()).toBe('/path/to/photo');
  });

  it('should handle camera errors', async () => {
    platform.isNative.set(true);

    spyOn(Camera, 'getPhoto').and.returnValue(Promise.reject(new Error('Camera denied')));

    await service.takePicture();

    expect(service.photoError()).toContain('Camera denied');
  });
});
```

## Test Coverage Goals

```typescript
// ✅ Target >80% coverage

// Unit tests
describe('Services', () => {
  // Test each method
  // Test error cases
  // Test edge cases
});

// Component tests
describe('Components', () => {
  // Test rendering
  // Test user interactions
  // Test signal updates
  // Test async operations
});

// Integration tests
describe('Workflows', () => {
  // Test complete user flows
  // Test component interactions
  // Test service integration
});
```

## DO's and DON'Ts

### DO ✅

- Test signal values directly: `store.events()`
- Test resource states: `hasValue()`, `hasError()`, `isLoading()`
- Use type guards in tests
- Test error cases
- Test edge cases
- Use `HttpClientTestingModule` for HTTP
- Mock Capacitor features
- Test async operations properly

### DON'T ❌

- Never subscribe to anything in tests
- Never forget `fixture.detectChanges()`
- Never test implementation details
- Never forget to call `httpMock.verify()`
- Never test RxJS patterns (use Promises)
- Never ignore error handling
- Never use real HTTP in tests
