using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Jobs;
using Sigil.Core.Protocol;
using Sigil.Storage.EfCore;
using Sigil.Storage.EfCore.Tests.Infrastructure;
using Xunit;

namespace Sigil.Storage.EfCore.Tests;

[Collection("SigilDb")]
public class EfContextStoreTests
{
    private readonly PostgresFixture _pg;
    public EfContextStoreTests(PostgresFixture pg) => _pg = pg;

    private async Task<JobId> SeedJob(string id)
    {
        await using var ctx = _pg.NewContext();
        var store = new EfJobStore(ctx);
        var jobId = new JobId(id);
        await store.CreateAsync(new Job { JobId = jobId });
        return jobId;
    }

    [Fact]
    public async Task GetSnapshotAsync_ReturnsEmptySnapshotForNewJob()
    {
        var jobId = await SeedJob("ctx-1");
        await using var ctx = _pg.NewContext();
        var store = new EfContextStore(ctx);

        var result = await store.GetSnapshotAsync(jobId);
        result.IsSuccess.ShouldBeTrue();
        result.Value.Snapshot.State.ShouldBeEmpty();
        result.Value.ETag.Value.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task CommitDeltaAsync_AppliesUpdates_WhenETagMatches()
    {
        var jobId = await SeedJob("ctx-2");

        ETag firstETag;
        await using (var ctx1 = _pg.NewContext())
        {
            var snap = await new EfContextStore(ctx1).GetSnapshotAsync(jobId);
            firstETag = snap.Value.ETag;
        }

        await using (var ctx2 = _pg.NewContext())
        {
            var commit = await new EfContextStore(ctx2).CommitDeltaAsync(
                jobId,
                new ContextDelta { Updates = new() { ["count"] = 1 } },
                firstETag);
            commit.IsSuccess.ShouldBeTrue();
        }

        await using (var ctx3 = _pg.NewContext())
        {
            var after = await new EfContextStore(ctx3).GetSnapshotAsync(jobId);
            after.Value.Snapshot.State["count"].ToString().ShouldBe("1");
            after.Value.ETag.ShouldNotBe(firstETag);
        }
    }

    [Fact]
    public async Task CommitDeltaAsync_ReturnsFailure_OnETagMismatch()
    {
        var jobId = await SeedJob("ctx-3");

        await using var ctx = _pg.NewContext();
        var store = new EfContextStore(ctx);
        var staleETag = new ETag("stale-tag-that-doesnt-match");

        var commit = await store.CommitDeltaAsync(
            jobId,
            new ContextDelta { Updates = new() { ["x"] = 1 } },
            staleETag);

        commit.IsFailure.ShouldBeTrue();
        commit.Error.ShouldBe(StorageErrors.EtagMismatch);
    }

    [Fact]
    public async Task ParallelCommits_OnlyOneSucceeds()
    {
        var jobId = await SeedJob("ctx-4");

        ETag etag;
        await using (var ctx0 = _pg.NewContext())
        {
            etag = (await new EfContextStore(ctx0).GetSnapshotAsync(jobId)).Value.ETag;
        }

        var t1 = Task.Run(async () =>
        {
            await using var c = _pg.NewContext();
            return await new EfContextStore(c).CommitDeltaAsync(
                jobId,
                new ContextDelta { Updates = new() { ["who"] = "A" } },
                etag);
        });
        var t2 = Task.Run(async () =>
        {
            await using var c = _pg.NewContext();
            return await new EfContextStore(c).CommitDeltaAsync(
                jobId,
                new ContextDelta { Updates = new() { ["who"] = "B" } },
                etag);
        });

        var results = await Task.WhenAll(t1, t2);
        results.Count(r => r.IsSuccess).ShouldBe(1);
        results.Count(r => r.IsFailure && r.Error == StorageErrors.EtagMismatch).ShouldBe(1);
    }

    [Fact]
    public async Task CommitDeltaAsync_AppliesRemovals()
    {
        var jobId = await SeedJob("ctx-5");

        await using (var ctx1 = _pg.NewContext())
        {
            var s = new EfContextStore(ctx1);
            var snap = await s.GetSnapshotAsync(jobId);
            await s.CommitDeltaAsync(
                jobId,
                new ContextDelta { Updates = new() { ["a"] = 1, ["b"] = 2 } },
                snap.Value.ETag);
        }

        await using (var ctx2 = _pg.NewContext())
        {
            var s = new EfContextStore(ctx2);
            var snap = await s.GetSnapshotAsync(jobId);
            var removed = await s.CommitDeltaAsync(
                jobId,
                new ContextDelta { Removals = new[] { "a" } },
                snap.Value.ETag);
            removed.IsSuccess.ShouldBeTrue();
        }

        await using (var ctx3 = _pg.NewContext())
        {
            var snap = await new EfContextStore(ctx3).GetSnapshotAsync(jobId);
            snap.Value.Snapshot.State.ContainsKey("a").ShouldBeFalse();
            snap.Value.Snapshot.State.ContainsKey("b").ShouldBeTrue();
        }
    }

    [Fact]
    public async Task AppendLogAsync_AppendsAndGetLogReturnsInOrder()
    {
        var jobId = await SeedJob("ctx-6");

        await using var ctx = _pg.NewContext();
        var store = new EfContextStore(ctx);
        await store.AppendLogAsync(jobId, new AgentLogEntry { Message = "first", AgentId = new AgentId("a") });
        await store.AppendLogAsync(jobId, new AgentLogEntry { Message = "second", AgentId = new AgentId("a") });

        await using var ctx2 = _pg.NewContext();
        var log = await new EfContextStore(ctx2).GetLogAsync(jobId);
        log.Select(x => x.Message).ShouldBe(new[] { "first", "second" });
    }
}
