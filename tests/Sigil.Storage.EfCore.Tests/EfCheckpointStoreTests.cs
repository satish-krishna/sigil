using Shouldly;
using Sigil.Core.Checkpoints;
using Sigil.Core.Identity;
using Sigil.Storage.EfCore;
using Sigil.Storage.EfCore.Tests.Infrastructure;
using Xunit;

namespace Sigil.Storage.EfCore.Tests;

[Collection("SigilDb")]
public class EfCheckpointStoreTests
{
    private readonly PostgresFixture _pg;
    public EfCheckpointStoreTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task CreateAsync_PersistsCheckpoint()
    {
        await using var ctx = _pg.NewContext();
        var store = new EfCheckpointStore(ctx);
        var cp = new Checkpoint { JobId = new JobId("cp-1"), StepId = new StepId("step-1") };

        var result = await store.CreateAsync(cp);
        result.IsSuccess.ShouldBeTrue();

        var fetched = await store.GetAsync(cp.CheckpointId);
        fetched.HasValue.ShouldBeTrue();
        fetched.Value.JobId.ShouldBe(new JobId("cp-1"));
    }

    [Fact]
    public async Task ResolveAsync_SetsStatusAndResolver()
    {
        await using var ctx = _pg.NewContext();
        var store = new EfCheckpointStore(ctx);
        var cp = new Checkpoint { JobId = new JobId("cp-2"), StepId = new StepId("step-2") };
        await store.CreateAsync(cp);

        var resolved = await store.ResolveAsync(cp.CheckpointId, CheckpointStatus.Approved, "alice");
        resolved.IsSuccess.ShouldBeTrue();

        var fetched = (await store.GetAsync(cp.CheckpointId)).Value;
        fetched.Status.ShouldBe(CheckpointStatus.Approved);
        fetched.ResolvedBy.ShouldBe("alice");
        fetched.ResolvedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetPendingForJobAsync_ReturnsOnlyPending()
    {
        await using var ctx = _pg.NewContext();
        var store = new EfCheckpointStore(ctx);
        var pending = new Checkpoint { JobId = new JobId("cp-3"), StepId = new StepId("s1") };
        var resolved = new Checkpoint { JobId = new JobId("cp-3"), StepId = new StepId("s2") };
        await store.CreateAsync(pending);
        await store.CreateAsync(resolved);
        await store.ResolveAsync(resolved.CheckpointId, CheckpointStatus.Approved, "bob");

        var found = await store.GetPendingForJobAsync(new JobId("cp-3"));
        found.Select(x => x.CheckpointId).ShouldContain(pending.CheckpointId);
        found.Select(x => x.CheckpointId).ShouldNotContain(resolved.CheckpointId);
    }
}
