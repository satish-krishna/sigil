using Microsoft.EntityFrameworkCore;
using Shouldly;
using Sigil.Storage.EfCore;
using Sigil.Storage.EfCore.Persistence;
using Xunit;

namespace Sigil.Storage.EfCore.Tests.Configuration;

public class ContextStateConfigTests
{
    private static SigilDbContext NewModelOnlyContext()
    {
        var opts = new DbContextOptionsBuilder<SigilDbContext>()
            .UseNpgsql("Host=unused")
            .Options;
        return new SigilDbContext(opts);
    }

    [Fact]
    public void ContextState_KeyIsJobId()
    {
        using var ctx = NewModelOnlyContext();
        var et = ctx.Model.FindEntityType(typeof(ContextStateRecord))!;
        et.FindPrimaryKey()!.Properties[0].Name.ShouldBe(nameof(ContextStateRecord.JobId));
    }

    [Fact]
    public void ETag_IsTextColumn_NoConcurrencyToken()
    {
        using var ctx = NewModelOnlyContext();
        var et = ctx.Model.FindEntityType(typeof(ContextStateRecord))!;
        var etag = et.FindProperty(nameof(ContextStateRecord.ETag))!;
        etag.GetColumnType().ShouldBe("text");
        etag.IsConcurrencyToken.ShouldBeFalse(
            "ETag-mismatch must surface as Result.Failure via ExecuteUpdate row-count, not DbUpdateConcurrencyException");
    }

    [Fact]
    public void State_IsJsonbColumn()
    {
        using var ctx = NewModelOnlyContext();
        var et = ctx.Model.FindEntityType(typeof(ContextStateRecord))!;
        et.FindProperty(nameof(ContextStateRecord.State))!.GetColumnType().ShouldBe("jsonb");
    }

    [Fact]
    public void Log_IsJsonbColumn()
    {
        using var ctx = NewModelOnlyContext();
        var et = ctx.Model.FindEntityType(typeof(ContextStateRecord))!;
        et.FindProperty(nameof(ContextStateRecord.Log))!.GetColumnType().ShouldBe("jsonb");
    }
}
