---
name: fastendpoints
description: |
  Master FastEndpoints API architecture for Siora backend projects. Provides patterns for endpoint structure, request/response handling, validation, and error handling. Use this skill when: (1) Creating new API endpoints; (2) Implementing request/response DTOs; (3) Adding input validation with FluentValidation; (4) Handling errors with Result pattern; (5) Implementing pre/post-processing; (6) Testing FastEndpoints
---

# Siora FastEndpoints Patterns

Lightweight, focused API endpoints with built-in validation and automatic documentation.

## Quick Start: Basic Endpoint

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
            .MaximumLength(200);

        RuleFor(x => x.StartDate)
            .NotEmpty()
            .GreaterThanOrEqualTo(DateTime.UtcNow).WithMessage("Start date must be in the future");

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate).WithMessage("End date must be after start date");
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

## Route Configuration

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

## Pre/Post Processing

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
        failures.Add(new("Name", "Test events not allowed"));
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

## Error Handling

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

```csharp
// Program.cs
builder.Services.AddFastEndpoints();

var app = builder.Build();

app.UseFastEndpoints(config =>
{
    config.Endpoints.RoutePrefix = "api";
});

app.Run();
```

## Complete Reference

See [fastendpoints-patterns.md](references/fastendpoints-patterns.md) for detailed patterns including:

- Key features and configuration
- Advanced validation
- Testing endpoints
- Error handling strategies
- Best practices

## Key Principles

✅ **DO**

- Keep endpoints focused on single concern
- Use validators for input validation
- Follow RESTful conventions
- Return appropriate HTTP status codes
- Log important events

❌ **DON'T**

- Never put business logic in endpoints (use services)
- Never mix multiple concerns
- Never ignore validation
- Never return sensitive data in errors
