using Microsoft.EntityFrameworkCore;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Jobs;
using Sigil.Storage.EfCore;
using Sigil.Storage.EfCore.Tests.Infrastructure;
using Xunit;

namespace Sigil.Storage.EfCore.Tests;

[Collection("SigilDb")]
public class EfJobStoreTests
{
    private readonly PostgresFixture _pg;
    public EfJobStoreTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task CreateAsync_PersistsJobAndPairedContextStateRow()
    {
        await using var ctx = _pg.NewContext();
        var store = new EfJobStore(ctx);
        var job = new Job { JobId = new JobId("job-1") };

        var result = await store.CreateAsync(job);
        result.IsSuccess.ShouldBeTrue();

        await using var verify = _pg.NewContext();
        var stored = await verify.Jobs.FindAsync(new JobId("job-1"));
        stored.ShouldNotBeNull();
        stored!.Status.ShouldBe(JobStatus.Pending);

        var ctxRow = await verify.ContextStates.FirstOrDefaultAsync(x => x.JobId == "job-1");
        ctxRow.ShouldNotBeNull("EfJobStore.CreateAsync must seed an empty ContextStateRecord");
        ctxRow!.ETag.ShouldNotBeNullOrEmpty();
        ctxRow.State.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAsync_ReturnsExistingJob()
    {
        await using var ctx = _pg.NewContext();
        var store = new EfJobStore(ctx);
        await store.CreateAsync(new Job { JobId = new JobId("job-2") });

        var fetched = await store.GetAsync(new JobId("job-2"));
        fetched.HasValue.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateStatusAsync_ChangesStatus()
    {
        await using var ctx = _pg.NewContext();
        var store = new EfJobStore(ctx);
        await store.CreateAsync(new Job { JobId = new JobId("job-3") });

        var result = await store.UpdateStatusAsync(new JobId("job-3"), JobStatus.Completed);
        result.IsSuccess.ShouldBeTrue();
        (await store.GetAsync(new JobId("job-3"))).Value.Status.ShouldBe(JobStatus.Completed);
    }
}
