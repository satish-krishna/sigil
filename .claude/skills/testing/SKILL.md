---
name: testing
description: |
  Master testing patterns for Siora projects (Angular + .NET). Provides patterns for unit tests, component tests, service tests, and integration tests. Use this skill when: (1) Testing signals with Jest; (2) Testing Resource API with HttpClientTestingModule; (3) Testing components with Spectator; (4) Testing forms and validation; (5) Testing .NET services with xUnit; (6) Achieving >80% code coverage
---

# Siora Testing Patterns

Complete testing strategies for Angular (Jest/Spectator) and .NET (xUnit).

## Testing Signals (Angular)

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

    expect(store.events()).toEqual(mockEvents);
  });

  it('should compute derived signal', () => {
    store.setEvents([
      { id: 1, name: 'Event 1', cancelled: false },
      { id: 2, name: 'Event 2', cancelled: true },
    ]);

    expect(store.filteredEvents().length).toBe(1);
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
    service.eventsResource.reload();

    expect(service.eventsResource.isLoading()).toBe(true);

    const req = httpMock.expectOne('/api/events');
    req.flush([
      { id: 1, name: 'Event 1' },
      { id: 2, name: 'Event 2' },
    ]);

    expect(service.eventsResource.hasValue()).toBe(true);
    expect(service.eventsResource.value().length).toBe(2);
  });

  it('should handle resource errors', () => {
    service.eventsResource.reload();

    const req = httpMock.expectOne('/api/events');
    req.error(new ErrorEvent('Network error'));

    expect(service.eventsResource.hasError()).toBe(true);
  });

  afterEach(() => {
    httpMock.verify();
  });
});
```

## Testing Components

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
    expect(component.form.invalid).toBe(true);

    component.form.patchValue({
      name: 'New Event',
      date: '2026-06-01',
    });

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

    expect(component.isSubmitting()).toBe(false);
  });
});
```

## .NET Testing (xUnit)

```csharp
// ✅ CORRECT: xUnit testing with FakeItEasy
public class EventServiceTests
{
    [Fact]
    public async Task CreateEvent_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var repository = A.Fake<IEventRepository>();
        var service = new EventService(repository);

        var request = new CreateEventRequest
        {
            Name = "Test Event",
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2)
        };

        // Act
        var result = await service.CreateEventAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Test Event");
    }

    [Fact]
    public async Task CreateEvent_WithInvalidRequest_ReturnsFailure()
    {
        // Arrange
        var repository = A.Fake<IEventRepository>();
        var service = new EventService(repository);

        var request = new CreateEventRequest
        {
            Name = "", // Invalid
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2)
        };

        // Act
        var result = await service.CreateEventAsync(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
```

## Coverage Goals

```typescript
// ✅ Target >80% coverage

// Unit tests - Each method
describe('Services', () => {
  // Test each method
  // Test error cases
  // Test edge cases
});

// Component tests - Rendering and interactions
describe('Components', () => {
  // Test rendering
  // Test user interactions
  // Test signal updates
  // Test async operations
});

// Integration tests - Complete workflows
describe('Workflows', () => {
  // Test complete user flows
  // Test component interactions
  // Test service integration
});
```

## Complete Reference

See [testing-patterns.md](references/testing-patterns.md) for detailed patterns including:

- Testing Capacitor features
- Testing async services with Promises
- Mocking strategies
- Performance testing
- Best practices and anti-patterns

## Key Principles

✅ **DO**

- Test signal values directly: `store.events()`
- Test resource states: `hasValue()`, `hasError()`, `isLoading()`
- Use type guards in tests
- Test error cases and edge cases
- Test async operations properly
- Mock Capacitor features
- Aim for >80% coverage

❌ **DON'T**

- Never subscribe to anything in tests
- Never forget `fixture.detectChanges()`
- Never test implementation details
- Never forget to call `httpMock.verify()`
- Never test RxJS patterns (use Promises)
- Never ignore error handling
- Never use real HTTP in tests
