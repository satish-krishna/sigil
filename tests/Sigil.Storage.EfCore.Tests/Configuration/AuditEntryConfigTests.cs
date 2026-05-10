using Microsoft.EntityFrameworkCore;
using Shouldly;
using Sigil.Core.Audit;
using Sigil.Storage.EfCore;
using Xunit;

namespace Sigil.Storage.EfCore.Tests.Configuration;

public class AuditEntryConfigTests
{
    private static SigilDbContext NewModelOnlyContext()
    {
        var opts = new DbContextOptionsBuilder<SigilDbContext>()
            .UseNpgsql("Host=unused")
            .Options;
        return new SigilDbContext(opts);
    }

    [Fact]
    public void AuditEntry_KeyIsAuditId()
    {
        using var ctx = NewModelOnlyContext();
        var et = ctx.Model.FindEntityType(typeof(AuditEntry))!;
        et.FindPrimaryKey()!.Properties[0].Name.ShouldBe(nameof(AuditEntry.AuditId));
    }

    [Fact]
    public void JobId_AgentId_AreIndexed()
    {
        using var ctx = NewModelOnlyContext();
        var et = ctx.Model.FindEntityType(typeof(AuditEntry))!;
        var indexes = et.GetIndexes().ToList();
        indexes.ShouldContain(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(AuditEntry.JobId));
        indexes.ShouldContain(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(AuditEntry.AgentId));
    }

    [Fact]
    public void Delta_IsOwned()
    {
        using var ctx = NewModelOnlyContext();
        var et = ctx.Model.FindEntityType(typeof(AuditEntry))!;
        var delta = et.FindNavigation(nameof(AuditEntry.Delta));
        delta.ShouldNotBeNull();
    }
}
