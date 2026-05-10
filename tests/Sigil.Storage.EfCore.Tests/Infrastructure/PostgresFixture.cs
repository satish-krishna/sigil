using Microsoft.EntityFrameworkCore;
using Sigil.Storage.EfCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sigil.Storage.EfCore.Tests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("sigil_test")
        .WithUsername("sigil")
        .WithPassword("sigil")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public SigilDbContext NewContext()
    {
        var opts = new DbContextOptionsBuilder<SigilDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new SigilDbContext(opts);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var ctx = NewContext();
        // Migration history doesn't exist yet (Task 15 lands later) — build schema
        // directly from the model. Switch to MigrateAsync() once migrations land.
        await ctx.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
