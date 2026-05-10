using Microsoft.EntityFrameworkCore;
using Shouldly;
using Sigil.Core.Identity;
using Xunit;

namespace Sigil.Storage.EfCore.Tests.Infrastructure;

[Collection("SigilDb")]
public class PostgresFixtureSmokeTests
{
    private readonly PostgresFixture _pg;
    public PostgresFixtureSmokeTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Fixture_ConnectsAndCreatesSchema()
    {
        await using var ctx = _pg.NewContext();

        var canConnect = await ctx.Database.CanConnectAsync();
        canConnect.ShouldBeTrue();

        // Querying a known-missing AgentId proves the table exists and is queryable
        // without depending on row count (other tests share the fixture).
        var probe = await ctx.AgentRegistrations
            .FirstOrDefaultAsync(x => x.AgentId == new AgentId("__smoke-probe-never-inserted__"));
        probe.ShouldBeNull();
    }
}
