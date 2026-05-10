using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Sigil.Storage.EfCore.Internal;

namespace Sigil.Storage.EfCore.Configuration;

internal sealed class AgentRegistrationConfig : IEntityTypeConfiguration<AgentRegistration>
{
    public void Configure(EntityTypeBuilder<AgentRegistration> e)
    {
        e.ToTable("agent_registrations");

        // PK — strongly-typed AgentId persisted as text.
        e.HasKey(x => x.AgentId);
        e.Property(x => x.AgentId)
            .HasConversion(v => v.Value, s => new AgentId(s))
            .HasColumnName("agent_id")
            .HasColumnType("text")
            .ValueGeneratedNever();

        e.Property(x => x.Name).HasColumnName("name").HasColumnType("text").IsRequired();
        e.Property(x => x.Domain).HasColumnName("domain").HasColumnType("text").IsRequired();
        e.Property(x => x.EndpointUrl).HasColumnName("endpoint_url").HasColumnType("text").IsRequired();
        e.Property(x => x.SemanticVersion).HasColumnName("semantic_version").HasColumnType("text").IsRequired();
        e.Property(x => x.RoutingWeight).HasColumnName("routing_weight").IsRequired();
        e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasColumnType("text").IsRequired();
        e.Property(x => x.MaxTokenBudget).HasColumnName("max_token_budget");
        e.Property(x => x.RegisteredAt).HasColumnName("registered_at").HasColumnType("timestamptz").IsRequired();
        e.Property(x => x.LastHeartbeat).HasColumnName("last_heartbeat").HasColumnType("timestamptz").IsRequired();

        // ModelSpec (owned, single row inline)
        e.OwnsOne(x => x.Model, m =>
        {
            m.Property(p => p.Provider).HasColumnName("model_provider").HasColumnType("text").IsRequired();
            m.Property(p => p.Model).HasColumnName("model_name").HasColumnType("text").IsRequired();
            m.OwnsOne(p => p.Sampling, s =>
            {
                s.Property(p => p.Temperature).HasColumnName("model_temperature");
                s.Property(p => p.TopP).HasColumnName("model_top_p");
                s.Property(p => p.MaxOutputTokens).HasColumnName("model_max_output_tokens");
            });
        });
        e.Navigation(x => x.Model).IsRequired();

        // Skills + Tools — jsonb with value converters.
        var skillsProp = e.Property(x => x.Skills)
            .HasColumnName("skills")
            .HasColumnType("jsonb")
            .IsRequired();
        skillsProp.Metadata.SetValueConverter(JsonValueConverters.ReadOnlyListConverter<Skill>());
        skillsProp.Metadata.SetValueComparer(JsonValueConverters.ReadOnlyListComparer<Skill>());

        var toolsProp = e.Property(x => x.Tools)
            .HasColumnName("tools")
            .HasColumnType("jsonb")
            .IsRequired();
        toolsProp.Metadata.SetValueConverter(JsonValueConverters.ReadOnlyListConverter<ToolBinding>());
        toolsProp.Metadata.SetValueComparer(JsonValueConverters.ReadOnlyListComparer<ToolBinding>());

        // SecurityProfile (owned, with jsonb AllowedTools).
        e.OwnsOne(x => x.Security, s =>
        {
            s.Property(p => p.CertificateThumbprint).HasColumnName("security_cert_thumbprint");
            s.Property(p => p.SigilKey).HasColumnName("security_sigil_key");
            s.Property(p => p.IsPiiCleared).HasColumnName("security_is_pii_cleared").IsRequired();
            s.Property(p => p.Tier)
                .HasColumnName("security_tier")
                .HasConversion<string>()
                .HasColumnType("text")
                .IsRequired();

            var allowed = s.Property(p => p.AllowedTools)
                .HasColumnName("security_allowed_tools")
                .HasColumnType("jsonb")
                .IsRequired();
            allowed.Metadata.SetValueConverter(JsonValueConverters.ReadOnlyListConverter<string>());
            allowed.Metadata.SetValueComparer(JsonValueConverters.ReadOnlyListComparer<string>());
        });

        // Metadata (owned, with jsonb Tags).
        e.OwnsOne(x => x.Metadata, m =>
        {
            var tags = m.Property(p => p.Tags)
                .HasColumnName("metadata_tags")
                .HasColumnType("jsonb")
                .IsRequired();
            tags.Metadata.SetValueConverter(JsonValueConverters.StringMapConverter());
            tags.Metadata.SetValueComparer(JsonValueConverters.StringMapComparer());
        });

        // GIN index for FindBySkillAsync — Postgres jsonb containment.
        e.HasIndex(x => x.Skills)
            .HasMethod("gin")
            .HasDatabaseName("ix_agent_registrations_skills_gin");

        e.HasIndex(x => x.Domain).HasDatabaseName("ix_agent_registrations_domain");
    }
}
