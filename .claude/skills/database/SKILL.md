---
name: database
description: |
  Master Entity Framework Core and Row-Level Security for Siora backend. Provides patterns for entity modeling, migrations, RLS policies, soft deletes, and query filters. Use this skill when: (1) Creating EF Core entities and configurations; (2) Writing migrations; (3) Implementing RLS policies; (4) Using soft deletes; (5) Configuring relationships and indexes; (6) Testing database operations
---

# Siora Database Patterns

Entity Framework Core handles schema with PostgreSQL Row-Level Security for data access control.

## Quick Start: Entity Modeling

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
}
```

## Entity Configuration

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

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Soft delete
        builder.HasQueryFilter(e => e.DeletedAt == null);

        // Relationships
        builder.HasOne(e => e.Host)
            .WithMany(u => u.HostedEvents)
            .HasForeignKey(e => e.HostId);

        // Indexes
        builder.HasIndex(e => e.HostId);
        builder.HasIndex(e => e.StartDate);
        builder.HasIndex(e => new { e.HostId, e.DeletedAt })
            .HasFilter("deleted_at IS NULL");

        builder.ToTable("events");
    }
}
```

## Creating Migrations

```bash
# From packages/backend directory
dotnet ef migrations add AddEventTable --project Siora.Infrastructure

# Apply migrations
dotnet ef database update --project Siora.Infrastructure

# List pending migrations
dotnet ef migrations list --project Siora.Infrastructure

# Generate SQL script
dotnet ef migrations script --project Siora.Infrastructure > migration.sql
```

## Migration Example

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
            });

        migrationBuilder.CreateIndex(
            name: "ix_events_host_id",
            table: "events",
            column: "host_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "events");
    }
}
```

## Row-Level Security (RLS)

```sql
-- Run in Supabase SQL Editor
ALTER TABLE events ENABLE ROW LEVEL SECURITY;

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
```

## Soft Deletes

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

## AppDbContext

```csharp
// Siora.Infrastructure/Data/AppDbContext.cs
public class AppDbContext : DbContext
{
    public DbSet<Event> Events { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
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

## Complete Reference

See [database-patterns.md](references/database-patterns.md) for detailed patterns including:

- Advanced entity configurations
- Complex relationship patterns
- Query optimization
- Testing with real databases
- Best practices and anti-patterns

## Key Principles

✅ **DO**

- Version every schema change via migrations
- Use EF Core migrations for all DDL changes
- Implement RLS policies in Supabase
- Include audit fields (CreatedAt, UpdatedAt, CreatedBy)
- Use soft deletes for important data
- Index frequently queried columns

❌ **DON'T**

- Never manually create tables (use migrations)
- Never hardcode SQL (use LINQ or EF Core)
- Never forget to apply migrations before deployment
- Never skip RLS configuration
- Never leave tables without indexes
