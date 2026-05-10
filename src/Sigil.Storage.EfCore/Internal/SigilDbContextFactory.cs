using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sigil.Storage.EfCore.Internal;

internal sealed class SigilDbContextFactory : IDesignTimeDbContextFactory<SigilDbContext>
{
    public SigilDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SIGIL_EFCORE_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=sigil_design;Username=sigil;Password=sigil";

        var opts = new DbContextOptionsBuilder<SigilDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new SigilDbContext(opts);
    }
}
