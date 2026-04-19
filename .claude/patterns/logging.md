# Logging Pattern - Serilog + Sentry

## Overview

Siora uses **Serilog** for structured logging on the backend with **Sentry Sink** for error tracking, and **Sentry** on the frontend for error monitoring.

**Benefits:**

- ✅ Structured logs (JSON format) for querying and analysis
- ✅ Automatic error tracking and alerting via Sentry
- ✅ Consistent logging across backend and frontend
- ✅ Never logs sensitive data (PII, secrets, tokens)
- ✅ Request correlation for tracing

## Backend Setup (Serilog + Sentry)

### Installation

```bash
cd packages/backend/Siora.Api

dotnet add package Serilog
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.Sentry
dotnet add package Serilog.Enrichers.Environment
dotnet add package Serilog.Enrichers.Property
```

### Program.cs Configuration

```csharp
using Serilog;
using Serilog.Enrichers.Property;
using Serilog.Sinks.Sentry;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)

    // Console output
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")

    // Sentry integration (error tracking)
    .WriteTo.Sentry(options =>
    {
        options.Dsn = builder.Configuration["Sentry:Dsn"]!;
        options.Environment = builder.Environment.EnvironmentName;
        options.MinimumEventLevel = LogEventLevel.Error;
        options.MinimumBreadcrumbLevel = LogEventLevel.Information;
        options.IncludeRequestPayload = false;
        options.IncludeTransactionData = true;

        // Don't log sensitive properties
        options.DestructureingDepthLimit = 4;
    })

    // Enrichers (add context to logs)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentUserName()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("Application", "Siora.Api")

    .CreateLogger();

builder.Host.UseSerilog();

try
{
    var app = builder.Build();

    // ... configure middleware ...

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

### appsettings.json

```json
{
  "Sentry": {
    "Dsn": "https://your-sentry-dsn@sentry.io/project-id",
    "Environment": "development",
    "SampleRate": 1.0,
    "TracesSampleRate": 1.0
  },
  "Serilog": {
    "MinimumLevel": "Information"
  }
}
```

## Logging Patterns

### Information (Expected Events)

```csharp
// Log important application events
Log.Information("User {UserId} created event {EventId}", userId, eventId);
Log.Information("Payment processed for order {OrderId} with amount {Amount}", orderId, amount);
Log.Information("Email sent to {Email} for event invitation", email);
```

### Warning (Potential Issues)

```csharp
// Log unexpected but recoverable situations
Log.Warning("Vendor {VendorId} has low review score: {Score}", vendorId, score);
Log.Warning("Event {EventId} has {Days} days remaining", eventId, daysRemaining);
Log.Warning("Large batch operation: {Count} items processed", itemCount);
```

### Error (Recoverable Failures)

```csharp
// Log errors and include context
Log.Error(ex, "Failed to send invitation email to {Email} for event {EventId}",
    email, eventId);

Log.Error("Payment processing failed for order {OrderId}: {Reason}",
    orderId, ex.Message);

// With structured data
Log.Error(ex, "Database update failed: {@Request}", updateRequest);
```

### Fatal (Unrecoverable Failures)

```csharp
// Log before terminating
Log.Fatal(ex, "Critical error: Database connection lost");
Log.Fatal(ex, "Configuration error: Missing required setting {Setting}", settingName);
```

## Structured Logging Best Practices

### ✅ DO: Log with Context

```csharp
// GOOD: Structured properties for querying
var @event = new Event { Id = eventId, Name = eventName };
Log.Information("Event created {@Event}", new
{
    @event.Id,
    @event.Name,
    UserId = userId,
    Timestamp = DateTime.UtcNow
});

// Query in Sentry: event.UserId:123 AND event.Name:Birthday
```

### ❌ DON'T: Log Sensitive Data

```csharp
// BAD: Never log passwords, tokens, API keys
Log.Information("User authenticated with password: {Password}", password); // ❌

// BAD: Avoid logging entire request objects with sensitive fields
Log.Information("Payment request: {@Request}", paymentRequest); // ❌ May include card number

// GOOD: Log only necessary non-sensitive info
Log.Information("User {UserId} authenticated successfully", userId); // ✅
Log.Information("Payment request for amount {Amount}", amount); // ✅ Amount is OK
```

### ✅ DO: Include Correlation IDs

```csharp
// Add correlation ID to all logs for request tracing
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].ToString()
            ?? Guid.NewGuid().ToString();

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            context.Response.Headers.Add("X-Correlation-ID", correlationId);
            await _next(context);
        }
    }
}

// Log with correlation ID automatically included
Log.Information("Processing event {EventId}", eventId);
// Output: {"CorrelationId":"abc-123", "EventId":"xyz-789", ...}
```

## Frontend Logging (Sentry)

### Installation

```bash
cd packages/frontend
npm install @sentry/angular
```

### Initialization (app.config.ts)

```typescript
import * as Sentry from '@sentry/angular';
import { environment } from './environments/environment';

Sentry.init({
  dsn: environment.sentryDsn,
  environment: environment.production ? 'production' : 'development',
  tracesSampleRate: 1.0,
  debug: !environment.production,

  // Session replay (optional)
  integrations: [
    new Sentry.Replay({
      maskAllText: true,
      blockAllMedia: true,
    }),
  ],
  replaysSessionSampleRate: 0.1,
  replaysOnErrorSampleRate: 1.0,
});

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    Sentry.traceAngularRouting(),
  ],
};
```

### Frontend Error Handling

```typescript
import * as Sentry from '@sentry/angular';

@Injectable({ providedIn: 'root' })
export class EventService {
  private http = inject(HttpClient);

  loadEvents(): Promise<Event[]> {
    return this.http
      .get<Event[]>('/api/events')
      .toPromise()
      .then((events) => {
        Sentry.captureMessage(`Loaded ${events.length} events`, 'info');
        return events;
      })
      .catch((error) => {
        // Automatically captures error in Sentry
        Sentry.captureException(error);
        throw error;
      });
  }
}
```

### Capturing User Context

```typescript
import * as Sentry from '@sentry/angular';

@Injectable({ providedIn: 'root' })
export class AuthService {
  signIn(user: User) {
    // Identify user in Sentry for better error tracking
    Sentry.setUser({
      id: user.id,
      email: user.email,
      username: user.name,
    });

    // Set user context
    Sentry.setTag('user_role', user.role);
    Sentry.setTag('user_plan', user.plan);
  }

  signOut() {
    Sentry.setUser(null);
  }
}
```

## environments.ts Configuration

```typescript
// environments/environment.ts (development)
export const environment = {
  production: false,
  sentryDsn: 'https://dev-key@sentry.io/project-dev',
};

// environments/environment.prod.ts (production)
export const environment = {
  production: true,
  sentryDsn: 'https://prod-key@sentry.io/project-prod',
};
```

## Monitoring & Alerting

### Sentry Setup

1. Create projects for frontend and backend
2. Get DSN from project settings
3. Set up alerts in Sentry dashboard:
   - Alert on Error (severity)
   - Alert on 404s
   - Alert on high error rate

### Log Levels in Production

```
Development:
- Debug: Detailed internal state
- Information: Application events
- Warning: Potential issues

Production:
- Information: Only important events
- Warning: Issues requiring attention
- Error: Send to Sentry + alert
- Fatal: Critical failures
```

## Best Practices

✅ **Do:**

- Use structured properties for queryable logs
- Include context for debugging (userIds, itemIds, etc.)
- Log entry and exit of critical operations
- Use appropriate log levels
- Include exceptions with full stack trace
- Set correlation IDs for request tracing

❌ **Don't:**

- Log passwords, API keys, tokens, PII
- Log raw request/response bodies
- Use string concatenation (use properties instead)
- Ignore exceptions (always log them)
- Log at Debug level in production
- Log the same event multiple times

## See Also

- [fastendpoints.md](./fastendpoints.md) - Logging in endpoints
- [database.md](./database.md) - Logging DB operations
- [testing.md](./testing.md) - Mocking logging in tests
