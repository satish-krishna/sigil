using Microsoft.EntityFrameworkCore;
using Sigil.Core.Audit;
using Sigil.Core.Checkpoints;
using Sigil.Core.Jobs;
using Sigil.Core.Registry;
using Sigil.Storage.EfCore.Persistence;

namespace Sigil.Storage.EfCore;

public sealed class SigilDbContext : DbContext
{
    public SigilDbContext(DbContextOptions<SigilDbContext> options) : base(options) { }

    public DbSet<AgentRegistration>      AgentRegistrations  => Set<AgentRegistration>();
    public DbSet<Job>                    Jobs                => Set<Job>();
    internal DbSet<ContextStateRecord>   ContextStates       => Set<ContextStateRecord>();
    public DbSet<Checkpoint>             Checkpoints         => Set<Checkpoint>();
    public DbSet<AuditEntry>             AuditEntries        => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SigilDbContext).Assembly);
    }
}
