# FastEndpoints Pattern

## Overview

FastEndpoints is a lightweight, focused alternative to traditional MVC controllers. Each endpoint is a self-contained class that handles a single request/response pair.

**Benefits:**

- ✅ Single responsibility per endpoint
- ✅ Minimal boilerplate (no controller overhead)
- ✅ Built-in validation, mapping, pre/post-processing
- ✅ Automatic Swagger/OpenAPI documentation
- ✅ Clean, testable code structure

## Basic Endpoint Structure

```csharp
// Request DTO
public class CreateEventRequest
{
    public string Name { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? MaxCapacity { get; set; }
}

// Response DTO
public class EventResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

// Validator
public class CreateEventValidator : Validator<CreateEventRequest>
{
    public CreateEventValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Event name is required")
            .MaximumLength(200).WithMessage("Event name must be 200 characters or less");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required")
            .GreaterThanOrEqualTo(DateTime.UtcNow).WithMessage("Start date must be in the future");

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate).WithMessage("End date must be after start date");

        RuleFor(x => x.MaxCapacity)
            .GreaterThan(0).When(x => x.MaxCapacity.HasValue)
            .WithMessage("Max capacity must be greater than 0");
    }
}

// Endpoint
public class CreateEventEndpoint : Endpoint<CreateEventRequest, EventResponse>
{
    private readonly EventService _eventService;

    public CreateEventEndpoint(EventService eventService)
    {
        _eventService = eventService;
    }

    public override void Configure()
    {
        Post("/api/events");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateEventRequest req, CancellationToken ct)
    {
        var result = await _eventService.CreateEventAsync(
            req.Name,
            req.StartDate,
            req.EndDate,
            req.MaxCapacity,
            ct
        );

        if (result.IsFailure)
        {
            await SendAsync(new { message = result.Error }, 400, cancellation: ct);
            return;
        }

        var response = new EventResponse
        {
            Id = result.Value.Id,
            Name = result.Value.Name,
            StartDate = result.Value.StartDate,
            EndDate = result.Value.EndDate
        };

        await SendCreatedAtAsync<GetEventEndpoint>(
            new { id = response.Id },
            response,
            generateAbsoluteUrl: true,
            cancellation: ct
        );
    }
}
```

## Key Features

### Route Configuration

```csharp
public override void Configure()
{
    Post("/api/events");                    // HTTP method + path
    AllowAnonymous();                        // Authorization
    RequirePermission("events:create");      // Policy/permission
    Throttle(perSecond: 10, duration: 60);  // Rate limiting
    ValidateAntiforgeryToken();              // CSRF protection
}
```

### Pre/Post Processing

```csharp
public override async Task PreProcessAsync(
    CreateEventRequest req,
    HttpContext ctx,
    List<ValidationFailure> failures,
    CancellationToken ct)
{
    // Run before validation and handler
    if (req.Name.StartsWith("Test"))
    {
        failures.Add(new("Name", "Test events not allowed in production"));
    }
}

public override async Task PostProcessAsync(
    CreateEventRequest req,
    EventResponse res,
    HttpContext ctx,
    Exception? exception,
    CancellationToken ct)
{
    // Run after handler, before response
    res.CreatedAt = DateTime.UtcNow;
}
```

### Error Handling

```csharp
public override async Task HandleAsync(CreateEventRequest req, CancellationToken ct)
{
    try
    {
        var result = await _service.CreateEventAsync(req, ct);

        if (result.IsFailure)
        {
            await SendAsync(new { error = result.Error }, 400, cancellation: ct);
            return;
        }

        await SendOkAsync(result.Value, cancellation: ct);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error creating event");
        await SendAsync(new { error = "Internal server error" }, 500, cancellation: ct);
    }
}
```

## Registration

In `Program.cs`:

```csharp
builder.Services.AddFastEndpoints();

var app = builder.Build();

app.UseFastEndpoints(config =>
{
    config.Endpoints.RoutePrefix = "api";
});

app.Run();
```

## Best Practices

✅ **Do:**

- Keep endpoints focused on a single concern
- Use validators for input validation
- Follow RESTful conventions
- Return appropriate HTTP status codes
- Log important events

❌ **Don't:**

- Put business logic in endpoints (use services)
- Mix multiple concerns in one endpoint
- Ignore validation
- Return sensitive data in error messages

## See Also

- [database.md](./database.md) - EF Core + RLS patterns
- [logging.md](./logging.md) - Serilog structured logging
- [testing.md](./testing.md) - Testing FastEndpoints
