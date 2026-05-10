using Microsoft.EntityFrameworkCore;
using Shouldly;
using Sigil.Core.Registry;
using Sigil.Storage.EfCore;
using Xunit;

namespace Sigil.Storage.EfCore.Tests.Configuration;

public class AgentRegistrationConfigTests
{
    private static SigilDbContext NewModelOnlyContext()
    {
        var opts = new DbContextOptionsBuilder<SigilDbContext>()
            .UseNpgsql("Host=unused")
            .Options;
        return new SigilDbContext(opts);
    }

    [Fact]
    public void AgentRegistration_KeyIsAgentIdString()
    {
        using var ctx = NewModelOnlyContext();
        var et = ctx.Model.FindEntityType(typeof(AgentRegistration))!;
        var pk = et.FindPrimaryKey()!;
        pk.Properties.ShouldHaveSingleItem();
        pk.Properties[0].Name.ShouldBe(nameof(AgentRegistration.AgentId));
    }

    [Fact]
    public void Skills_IsJsonbColumn()
    {
        using var ctx = NewModelOnlyContext();
        var et = ctx.Model.FindEntityType(typeof(AgentRegistration))!;
        var skillsProp = et.FindProperty(nameof(AgentRegistration.Skills))!;
        skillsProp.GetColumnType().ShouldBe("jsonb");
    }

    [Fact]
    public void Tools_IsJsonbColumn()
    {
        using var ctx = NewModelOnlyContext();
        var et = ctx.Model.FindEntityType(typeof(AgentRegistration))!;
        var toolsProp = et.FindProperty(nameof(AgentRegistration.Tools))!;
        toolsProp.GetColumnType().ShouldBe("jsonb");
    }

    [Fact]
    public void Skills_HasGinIndex()
    {
        using var ctx = NewModelOnlyContext();
        var et = ctx.Model.FindEntityType(typeof(AgentRegistration))!;
        et.GetIndexes().ShouldContain(
            i => i.Properties.Count == 1
              && i.Properties[0].Name == nameof(AgentRegistration.Skills),
            customMessage: "Expected a GIN index on Skills for jsonb containment queries");
    }
}
