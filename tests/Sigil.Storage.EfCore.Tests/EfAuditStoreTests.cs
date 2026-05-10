using Shouldly;
using Sigil.Core.Audit;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Sigil.Storage.EfCore;
using Sigil.Storage.EfCore.Tests.Infrastructure;
using Xunit;

namespace Sigil.Storage.EfCore.Tests;

[Collection("SigilDb")]
public class EfAuditStoreTests
{
    private readonly PostgresFixture _pg;
    public EfAuditStoreTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task LogChangeAsync_PersistsTwoRowsForIdenticalContent()
    {
        await using var ctx = _pg.NewContext();
        var store = new EfAuditStore(ctx);
        var jobId = new JobId("audit-1");
        var agentId = new AgentId("a");
        var stepId = new StepId("s");

        var entry1 = new AuditEntry { JobId = jobId, AgentId = agentId, StepId = stepId };
        var entry2 = new AuditEntry { JobId = jobId, AgentId = agentId, StepId = stepId };
        await store.LogChangeAsync(entry1);
        await store.LogChangeAsync(entry2);

        var history = await store.GetHistoryAsync(jobId);
        history.Count.ShouldBe(2);
        var distinctIds = history.Select(x => x.AuditId).ToHashSet();
        distinctIds.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetHistoryAsync_FiltersByJob()
    {
        await using var ctx = _pg.NewContext();
        var store = new EfAuditStore(ctx);
        await store.LogChangeAsync(new AuditEntry { JobId = new JobId("audit-2"), AgentId = new AgentId("a"), StepId = new StepId("s") });
        await store.LogChangeAsync(new AuditEntry { JobId = new JobId("audit-3"), AgentId = new AgentId("a"), StepId = new StepId("s") });

        var history = await store.GetHistoryAsync(new JobId("audit-2"));
        history.Count.ShouldBe(1);
        history[0].JobId.ShouldBe(new JobId("audit-2"));
    }

    [Fact]
    public async Task GetAgentHistoryAsync_FiltersByAgent()
    {
        await using var ctx = _pg.NewContext();
        var store = new EfAuditStore(ctx);
        await store.LogChangeAsync(new AuditEntry { JobId = new JobId("audit-4"), AgentId = new AgentId("zeta"), StepId = new StepId("s") });
        await store.LogChangeAsync(new AuditEntry { JobId = new JobId("audit-5"), AgentId = new AgentId("zeta"), StepId = new StepId("s") });
        await store.LogChangeAsync(new AuditEntry { JobId = new JobId("audit-6"), AgentId = new AgentId("other"), StepId = new StepId("s") });

        var history = await store.GetAgentHistoryAsync(new AgentId("zeta"));
        history.Count.ShouldBe(2);
        history.ShouldAllBe(x => x.AgentId == new AgentId("zeta"));
    }
}
