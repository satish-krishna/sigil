using Microsoft.EntityFrameworkCore;
using Shouldly;
using Sigil.Core.Jobs;
using Sigil.Storage.EfCore;
using Xunit;

namespace Sigil.Storage.EfCore.Tests.Configuration;

public class JobConfigTests
{
    private static SigilDbContext NewModelOnlyContext()
    {
        var opts = new DbContextOptionsBuilder<SigilDbContext>()
            .UseNpgsql("Host=unused")
            .Options;
        return new SigilDbContext(opts);
    }

    [Fact]
    public void Job_KeyIsJobIdString()
    {
        using var ctx = NewModelOnlyContext();
        var et = ctx.Model.FindEntityType(typeof(Job))!;
        var pk = et.FindPrimaryKey()!;
        pk.Properties[0].Name.ShouldBe(nameof(Job.JobId));
    }

    [Fact]
    public void Status_IsString()
    {
        using var ctx = NewModelOnlyContext();
        var et = ctx.Model.FindEntityType(typeof(Job))!;
        et.FindProperty(nameof(Job.Status))!.GetColumnType().ShouldBe("text");
    }
}
