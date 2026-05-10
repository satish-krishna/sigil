using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Core.Identity;
using Sigil.Core.Jobs;

namespace Sigil.Storage.EfCore.Configuration;

internal sealed class JobConfig : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> e)
    {
        e.ToTable("jobs");

        e.HasKey(x => x.JobId);
        e.Property(x => x.JobId)
            .HasConversion(v => v.Value, s => new JobId(s))
            .HasColumnName("job_id")
            .HasColumnType("text")
            .ValueGeneratedNever();

        e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasColumnType("text").IsRequired();
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
    }
}
