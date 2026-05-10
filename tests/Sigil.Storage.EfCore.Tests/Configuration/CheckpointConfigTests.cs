using Microsoft.EntityFrameworkCore;
using Shouldly;
using Sigil.Core.Checkpoints;
using Sigil.Storage.EfCore;
using Xunit;

namespace Sigil.Storage.EfCore.Tests.Configuration;

public class CheckpointConfigTests
{
    private static SigilDbContext NewModelOnlyContext()
    {
        var opts = new DbContextOptionsBuilder<SigilDbContext>()
            .UseNpgsql("Host=unused")
            .Options;
        return new SigilDbContext(opts);
    }

    [Fact]
    public void Checkpoint_KeyIsCheckpointId()
    {
        using var ctx = NewModelOnlyContext();
        var et = ctx.Model.FindEntityType(typeof(Checkpoint))!;
        et.FindPrimaryKey()!.Properties[0].Name.ShouldBe(nameof(Checkpoint.CheckpointId));
    }

    [Fact]
    public void JobId_IsIndexed()
    {
        using var ctx = NewModelOnlyContext();
        var et = ctx.Model.FindEntityType(typeof(Checkpoint))!;
        et.GetIndexes().ShouldContain(i =>
            i.Properties.Count == 1 && i.Properties[0].Name == nameof(Checkpoint.JobId));
    }
}
