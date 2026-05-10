using Shouldly;
using Sigil.Core.Storage;
using Sigil.Storage.EfCore;
using Sigil.Storage.EfCore.Tests.Infrastructure;
using Xunit;

namespace Sigil.Storage.EfCore.Tests;

[Collection("SigilDb")]
public class EfSigilStoreTests
{
    private readonly PostgresFixture _pg;
    public EfSigilStoreTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public void EfSigilStore_ExposesAllFiveSubStores()
    {
        using var ctx = _pg.NewContext();
        ISigilStore store = new EfSigilStore(
            new EfAgentRegistrationStore(ctx),
            new EfJobStore(ctx),
            new EfContextStore(ctx),
            new EfCheckpointStore(ctx),
            new EfAuditStore(ctx));

        store.Agents.ShouldNotBeNull();
        store.Jobs.ShouldNotBeNull();
        store.Contexts.ShouldNotBeNull();
        store.Checkpoints.ShouldNotBeNull();
        store.Audit.ShouldNotBeNull();
    }
}
