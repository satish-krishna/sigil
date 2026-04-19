---
name: logging
description: |
  Master Serilog structured logging and Sentry error tracking for Siora projects. Provides patterns for structured logging, error tracking, sensitive data protection, and correlation IDs. Use this skill when: (1) Setting up Serilog on backend; (2) Configuring Sentry integration; (3) Writing structured logs with context; (4) Protecting sensitive data in logs; (5) Setting up frontend Sentry; (6) Implementing correlation IDs for request tracing
---

# Siora Logging Patterns

Structured logging with Serilog on backend and Sentry for error tracking on both frontend and backend.

## Quick Start: Backend Setup (Serilog)

```csharp
// Program.cs
using Serilog;
using Serilog.Sinks.Sentry;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)

    // Console output
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")

    // Sentry integration
    .WriteTo.Sentry(options =>
    {
        options.Dsn = builder.Configuration["Sentry:Dsn"]!;
        options.Environment = builder.Environment.EnvironmentName;
        options.MinimumEventLevel = LogEventLevel.Error;
    })

    // Enrichers
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentUserName()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("Application", "Siora.Api")

    .CreateLogger();

builder.Host.UseSerilog();
```

## Logging Patterns

```csharp
// ✅ Information (Expected Events)
Log.Information("User {UserId} created event {EventId}", userId, eventId);

// ✅ Warning (Potential Issues)
Log.Warning("Event {EventId} has {Days} days remaining", eventId, daysRemaining);

// ✅ Error (Recoverable Failures)
Log.Error(ex, "Failed to send email to {Email} for event {EventId}", email, eventId);

// ✅ Fatal (Unrecoverable Failures)
Log.Fatal(ex, "Critical error: Database connection lost");
```

## Structured Logging with Context

```csharp
// ✅ GOOD: Structured properties for querying
var @event = new Event { Id = eventId, Name = eventName };
Log.Information("Event created {@Event}", new
{
    @event.Id,
    @event.Name,
    UserId = userId,
    Timestamp = DateTime.UtcNow
});

// ❌ BAD: Never log sensitive data
Log.Information("User authenticated with password: {Password}", password); // ❌
```

## Correlation IDs

```csharp
// Middleware for correlation IDs
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

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

```typescript
// Installation
// npm install @sentry/angular

// app.config.ts
import * as Sentry from '@sentry/angular';

Sentry.init({
  dsn: environment.sentryDsn,
  environment: environment.production ? 'production' : 'development',
  tracesSampleRate: 1.0,
  integrations: [
    new Sentry.Replay({
      maskAllText: true,
      blockAllMedia: true,
    }),
  ],
});

export const appConfig: ApplicationConfig = {
  providers: [provideRouter(routes), Sentry.traceAngularRouting()],
};
```

## Frontend Error Handling

```typescript
// ✅ Automatically captures errors
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
        Sentry.captureException(error);
        throw error;
      });
  }
}
```

## User Context

```typescript
// Identify users in Sentry
@Injectable({ providedIn: 'root' })
export class AuthService {
  signIn(user: User) {
    Sentry.setUser({
      id: user.id,
      email: user.email,
      username: user.name,
    });

    Sentry.setTag('user_role', user.role);
  }

  signOut() {
    Sentry.setUser(null);
  }
}
```

## appsettings.json

```json
{
  "Sentry": {
    "Dsn": "https://your-sentry-dsn@sentry.io/project-id",
    "Environment": "development",
    "SampleRate": 1.0
  }
}
```

## Complete Reference

See [logging-patterns.md](references/logging-patterns.md) for detailed patterns including:

- Installation and configuration
- Sensitive data protection
- Frontend session replay
- Monitoring and alerting setup
- Best practices and anti-patterns

## Key Principles

✅ **DO**

- Use structured properties for queryable logs
- Include context for debugging
- Log entry and exit of critical operations
- Use appropriate log levels
- Set correlation IDs for request tracing
- Protect sensitive data

❌ **DON'T**

- Never log passwords, API keys, tokens, PII
- Never log raw request/response bodies
- Never use string concatenation (use properties)
- Never ignore exceptions
- Never log at Debug level in production
