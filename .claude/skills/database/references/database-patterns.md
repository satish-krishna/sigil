# Database Pattern - EF Core + RLS

## Overview

Siora uses **Entity Framework Core** for schema management with **EF Core Migrations** and **PostgreSQL Row-Level Security (RLS)** for data access control via Supabase DDLs.

**Architecture:**

- ✅ EF Core handles schema (tables, relationships, indexes)
- ✅ Migrations versioned and tracked (code-first)
- ✅ RLS policies manage data access (database-level security)
- ✅ Audit fields for compliance (CreatedAt, UpdatedAt, DeletedAt)

## Entity Modeling

### Basic Entity with Audit Fields

```csharp
// Siora.Core/Domain/Event.cs
public class Event
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? MaxCapacity { get; set; }

    // Audit Fields
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } // User ID from Supabase
    public DateTime? DeletedAt { get; set; } // Soft delete

    // Navigation
    public Guid HostId { get; set; }
    public User Host { get; set; }
    public ICollection<EventProposal> Proposals { get; set; }
    public ICollection<RSVP> RSVPs { get; set; }
}
```

### Entity Configuration (FluentAPI)

```csharp
// Siora.Infrastructure/Data/Config/EventConfiguration.cs
public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(5000);

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.CreatedBy)
            .IsRequired();

        // Soft delete (IMPORTANT: Always filter in queries!)
        builder.HasQueryFilter(e => e.DeletedAt == null);

        // Relationships
        builder.HasOne(e => e.Host)
            .WithMany(u => u.HostedEvents)
            .HasForeignKey(e => e.HostId);

        builder.HasMany(e => e.Proposals)
            .WithOne(p => p.Event)
            .HasForeignKey(p => p.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.RSVPs)
            .WithOne(r => r.Event)
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(e => e.HostId);
        builder.HasIndex(e => e.StartDate);
        builder.HasIndex(e => new { e.HostId, e.DeletedAt })
            .HasFilter("deleted_at IS NULL");

        // Table name
        builder.ToTable("events");
    }
}
```

## Migrations (EF Core)

### Creating a Migration

```bash
# From packages/backend directory
dotnet ef migrations add AddEventTable --project Siora.Infrastructure

# Output creates:
# Siora.Infrastructure/Migrations/{Timestamp}_AddEventTable.cs
```

### Migration Example

```csharp
// Siora.Infrastructure/Migrations/20260110000000_AddEventTable.cs
public partial class AddEventTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "events",
            columns: table => new
            {
                id = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
                name = table.Column<string>(maxLength: 200, nullable: false),
                description = table.Column<string>(maxLength: 5000),
                start_date = table.Column<DateTime>(nullable: false),
                end_date = table.Column<DateTime>(nullable: false),
                max_capacity = table.Column<int>(),
                host_id = table.Column<Guid>(nullable: false),
                created_at = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                updated_at = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                created_by = table.Column<string>(nullable: false),
                deleted_at = table.Column<DateTime>()
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_events", x => x.id);
                table.ForeignKey("fk_events_users_host_id", x => x.host_id, "users", "id");
            });

        migrationBuilder.CreateIndex(
            name: "ix_events_host_id",
            table: "events",
            column: "host_id");

        migrationBuilder.CreateIndex(
            name: "ix_events_start_date",
            table: "events",
            column: "start_date");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "events");
    }
}
```

### Applying Migrations

```bash
# Apply pending migrations to development database
dotnet ef database update --project Siora.Infrastructure

# List pending migrations
dotnet ef migrations list --project Siora.Infrastructure

# Remove last migration (before applying)
dotnet ef migrations remove --project Siora.Infrastructure

# Generate idempotent SQL script (for production deployment)
dotnet ef migrations script --project Siora.Infrastructure > migration.sql
```

## Row-Level Security (RLS) - Supabase DDL

### Enabling RLS

```sql
-- Run in Supabase SQL Editor
-- Enable RLS on the table
ALTER TABLE events ENABLE ROW LEVEL SECURITY;

-- Allow admins to see all rows
CREATE POLICY "admins_see_all_events"
  ON events
  FOR SELECT
  USING (auth.jwt() ->> 'is_admin' = 'true');

-- Users see events they host or are invited to
CREATE POLICY "users_see_own_events"
  ON events
  FOR SELECT
  USING (
    created_by = auth.uid()::text
    OR EXISTS (
      SELECT 1 FROM event_guests
      WHERE event_id = events.id
      AND user_id = auth.uid()::text
    )
  );

-- Users can update their own events
CREATE POLICY "users_update_own_events"
  ON events
  FOR UPDATE
  USING (created_by = auth.uid()::text)
  WITH CHECK (created_by = auth.uid()::text);

-- Prevent deletion (use soft delete via updated row instead)
CREATE POLICY "no_delete_events"
  ON events
  FOR DELETE
  USING (false);
```

### Important RLS Patterns

```sql
-- Filter by authenticated user
USING (created_by = auth.uid()::text)

-- Filter by role
USING (auth.jwt() ->> 'role' = 'admin')

-- Complex conditions (with relationships)
USING (
  id IN (
    SELECT event_id FROM event_proposals
    WHERE vendor_id = auth.uid()::text
  )
)

-- Time-based access
USING (
  created_by = auth.uid()::text
  AND created_at > now() - interval '30 days'
)
```

## AppDbContext

```csharp
// Siora.Infrastructure/Data/AppDbContext.cs
public class AppDbContext : DbContext
{
    private readonly ILoggerFactory _loggerFactory;

    public AppDbContext(DbContextOptions<AppDbContext> options, ILoggerFactory loggerFactory)
        : base(options)
    {
        _loggerFactory = loggerFactory;
    }

    public DbSet<Event> Events { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<EventProposal> Proposals { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseLoggerFactory(_loggerFactory);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Use snake_case for column names
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ConvertToSnakeCase(property.GetColumnName()));
            }
        }
    }

    private static string ConvertToSnakeCase(string input)
    {
        return Regex.Replace(input, @"([A-Z])", "_$1").ToLower().TrimStart('_');
    }
}
```

## Soft Deletes Pattern

```csharp
// Service using soft delete
public async Task DeleteEventAsync(Guid eventId, CancellationToken ct)
{
    var @event = await _context.Events.FindAsync(new object[] { eventId }, cancellationToken: ct);

    if (@event == null)
        return Result.Failure("Event not found");

    // Soft delete: set DeletedAt instead of removing
    @event.DeletedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync(ct);

    return Result.Success();
}

// Query automatically excludes soft-deleted rows
public async Task<List<Event>> GetEventsByUserAsync(string userId, CancellationToken ct)
{
    return await _context.Events
        .Where(e => e.CreatedBy == userId)
        // .HasQueryFilter automatically applies: .Where(e => e.DeletedAt == null)
        .ToListAsync(ct);
}
```

## Best Practices

✅ **Do:**

- Version every schema change via migrations
- Use EF Core migrations for all DDL changes
- Implement RLS policies in Supabase for security
- Include audit fields (CreatedAt, UpdatedAt, CreatedBy)
- Use soft deletes for important data
- Index frequently queried columns
- Use query filters for soft delete

❌ **Don't:**

- Manually create tables (use migrations)
- Hardcode SQL (use LINQ or EF Core)
- Forget to apply migrations before deployment
- Store sensitive data unencrypted
- Skip RLS configuration
- Leave tables without indexes

## See Also

- [fastendpoints.md](./fastendpoints.md) - API endpoint patterns
- [logging.md](./logging.md) - Logging database operations
- [testing.md](./testing.md) - Testing with real databases
