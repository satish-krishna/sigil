using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Core.Checkpoints;
using Sigil.Core.Identity;

namespace Sigil.Storage.EfCore.Configuration;

internal sealed class CheckpointConfig : IEntityTypeConfiguration<Checkpoint>
{
    public void Configure(EntityTypeBuilder<Checkpoint> e)
    {
        e.ToTable("checkpoints");

        e.HasKey(x => x.CheckpointId);
        e.Property(x => x.CheckpointId).HasColumnName("checkpoint_id").HasColumnType("text").IsRequired();

        e.Property(x => x.JobId)
            .HasConversion(v => v.Value, s => new JobId(s))
            .HasColumnName("job_id")
            .HasColumnType("text")
            .IsRequired();

        e.Property(x => x.StepId)
            .HasConversion(v => v.Value, s => new StepId(s))
            .HasColumnName("step_id")
            .HasColumnType("text")
            .IsRequired();

        e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasColumnType("text").IsRequired();
        e.Property(x => x.ResolvedBy).HasColumnName("resolved_by").HasColumnType("text");
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        e.Property(x => x.ResolvedAt).HasColumnName("resolved_at").HasColumnType("timestamptz");
    }
}
