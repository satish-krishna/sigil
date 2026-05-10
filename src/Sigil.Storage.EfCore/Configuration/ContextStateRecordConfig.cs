using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Storage.EfCore.Internal;
using Sigil.Storage.EfCore.Persistence;

namespace Sigil.Storage.EfCore.Configuration;

internal sealed class ContextStateRecordConfig : IEntityTypeConfiguration<ContextStateRecord>
{
    public void Configure(EntityTypeBuilder<ContextStateRecord> e)
    {
        e.ToTable("context_states");

        e.HasKey(x => x.JobId);
        e.Property(x => x.JobId).HasColumnName("job_id").HasColumnType("text").IsRequired();
        e.Property(x => x.ETag).HasColumnName("etag").HasColumnType("text").IsRequired();

        var state = e.Property(x => x.State)
            .HasColumnName("state")
            .HasColumnType("jsonb")
            .IsRequired();
        state.Metadata.SetValueConverter(JsonValueConverters.ObjectMapConverter());
        state.Metadata.SetValueComparer(JsonValueConverters.ObjectMapComparer());

        var log = e.Property(x => x.Log)
            .HasColumnName("log")
            .HasColumnType("jsonb")
            .IsRequired();
        log.Metadata.SetValueConverter(JsonValueConverters.ReadOnlyListConverter<Sigil.Core.Protocol.AgentLogEntry>());
        log.Metadata.SetValueComparer(JsonValueConverters.ReadOnlyListComparer<Sigil.Core.Protocol.AgentLogEntry>());
    }
}
