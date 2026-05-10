using Microsoft.EntityFrameworkCore;
using Shouldly;
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

        // Schema was created in InitializeAsync via EnsureCreatedAsync.
        // CanConnect proves the container is reachable.
        var canConnect = await ctx.Database.CanConnectAsync();
        canConnect.ShouldBeTrue();

        // The agent_registrations table exists if EnsureCreated worked end-to-end.
        // Querying an empty set should succeed and return zero rows.
        var count = await ctx.AgentRegistrations.CountAsync();
        count.ShouldBe(0);
    }
}
