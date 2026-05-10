using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Core.Audit;
using Sigil.Core.Identity;
using Sigil.Storage.EfCore.Internal;

namespace Sigil.Storage.EfCore.Configuration;

internal sealed class AuditEntryConfig : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> e)
    {
        e.ToTable("audit_entries");

        e.HasKey(x => x.AuditId);
        e.Property(x => x.AuditId).HasColumnName("audit_id").HasColumnType("text").IsRequired();

        e.Property(x => x.JobId)
            .HasConversion(v => v.Value, s => new JobId(s))
            .HasColumnName("job_id")
            .HasColumnType("text")
            .IsRequired();

        e.Property(x => x.AgentId)
            .HasConversion(v => v.Value, s => new AgentId(s))
            .HasColumnName("agent_id")
            .HasColumnType("text")
            .IsRequired();

        e.Property(x => x.StepId)
            .HasConversion(v => v.Value, s => new StepId(s))
            .HasColumnName("step_id")
            .HasColumnType("text")
            .IsRequired();

        e.Property(x => x.Timestamp).HasColumnName("timestamp").HasColumnType("timestamptz").IsRequired();

        // ContextDelta (owned, persisted as jsonb)
        e.OwnsOne(x => x.Delta, d =>
        {
            var updates = d.Property(p => p.Updates)
                .HasColumnName("delta_updates")
                .HasColumnType("jsonb")
                .IsRequired();
            updates.Metadata.SetValueConverter(JsonValueConverters.MutableObjectMapConverter());
            updates.Metadata.SetValueComparer(JsonValueConverters.MutableObjectMapComparer());

            var removals = d.Property(p => p.Removals)
                .HasColumnName("delta_removals")
                .HasColumnType("jsonb")
                .IsRequired();
            removals.Metadata.SetValueConverter(JsonValueConverters.StringArrayConverter());
            removals.Metadata.SetValueComparer(JsonValueConverters.StringArrayComparer());
        });

        // UsageMetrics (owned, with jsonb Custom map)
        e.OwnsOne(x => x.Metrics, m =>
        {
            m.Property(p => p.PromptTokens).HasColumnName("metrics_prompt_tokens").IsRequired();
            m.Property(p => p.CompletionTokens).HasColumnName("metrics_completion_tokens").IsRequired();
            m.Property(p => p.Duration).HasColumnName("metrics_duration_ticks")
                .HasConversion(v => v.Ticks, t => TimeSpan.FromTicks(t))
                .IsRequired();

            var custom = m.Property(p => p.Custom)
                .HasColumnName("metrics_custom")
                .HasColumnType("jsonb")
                .IsRequired();
            custom.Metadata.SetValueConverter(JsonValueConverters.ObjectMapConverter());
            custom.Metadata.SetValueComparer(JsonValueConverters.ObjectMapComparer());
        });
    }
}
