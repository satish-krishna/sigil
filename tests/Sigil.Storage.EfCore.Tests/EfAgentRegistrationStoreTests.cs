using CSharpFunctionalExtensions;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Sigil.Storage.EfCore;
using Sigil.Storage.EfCore.Tests.Infrastructure;
using Xunit;

namespace Sigil.Storage.EfCore.Tests;

[Collection("SigilDb")]
public class EfAgentRegistrationStoreTests
{
    private readonly PostgresFixture _pg;
    public EfAgentRegistrationStoreTests(PostgresFixture pg) => _pg = pg;

    private static AgentRegistration Sample(string id = "echo-agent", params string[] skillNames) =>
        new()
        {
            AgentId = new AgentId(id),
            Name = id,
            Domain = "test",
            EndpointUrl = "https://localhost",
            Model = new ModelSpec { Provider = "openai", Model = "gpt-4o-mini" },
            Skills = skillNames.Length == 0
                ? new[] { new Skill { Name = "echo", Description = "echo" } }
                : skillNames.Select(n => new Skill { Name = n, Description = n }).ToArray(),
            Tools = Array.Empty<ToolBinding>(),
        };

    [Fact]
    public async Task RegisterAsync_PersistsAndGetReturnsEqualValue()
    {
        await using var ctx = _pg.NewContext();
        var store = new EfAgentRegistrationStore(ctx);
        var reg = Sample("a-1", "skill-a");

        var registerResult = await store.RegisterAsync(reg);
        registerResult.IsSuccess.ShouldBeTrue();

        var fetched = await store.GetAsync(new AgentId("a-1"));
        fetched.HasValue.ShouldBeTrue();
        fetched.Value.AgentId.ShouldBe(new AgentId("a-1"));
        fetched.Value.Skills.Single().Name.ShouldBe("skill-a");
    }

    [Fact]
    public async Task FindBySkillAsync_ReturnsAgentsAdvertisingThatSkill()
    {
        await using var ctx = _pg.NewContext();
        var store = new EfAgentRegistrationStore(ctx);
        await store.RegisterAsync(Sample("a-2", "summarize-pdf"));
        await store.RegisterAsync(Sample("a-3", "transcribe-audio"));

        var matches = await store.FindBySkillAsync("summarize-pdf");
        matches.Select(x => x.AgentId.Value).ShouldContain("a-2");
        matches.Select(x => x.AgentId.Value).ShouldNotContain("a-3");
    }

    [Fact]
    public async Task RegisterAsync_RejectsDuplicateSkillNameWithinAgent()
    {
        await using var ctx = _pg.NewContext();
        var store = new EfAgentRegistrationStore(ctx);
        var reg = Sample("a-4") with
        {
            Skills = new[]
            {
                new Skill { Name = "dup", Description = "1" },
                new Skill { Name = "dup", Description = "2" }
            }
        };

        var result = await store.RegisterAsync(reg);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(StorageErrors.ValidationSkillDuplicate);
    }

    [Fact]
    public async Task RegisterAsync_RejectsSkillRequiringUnknownTool()
    {
        await using var ctx = _pg.NewContext();
        var store = new EfAgentRegistrationStore(ctx);
        var reg = Sample("a-5") with
        {
            Skills = new[]
            {
                new Skill { Name = "needs-tool", Description = "x", RequiredTools = new[] { "missing-tool" } }
            },
            Tools = Array.Empty<ToolBinding>()
        };

        var result = await store.RegisterAsync(reg);
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(StorageErrors.ValidationSkillRequiresUnknownTool);
    }

    [Fact]
    public async Task UpdateHeartbeatAsync_BumpsLastHeartbeat()
    {
        await using var ctx = _pg.NewContext();
        var store = new EfAgentRegistrationStore(ctx);
        await store.RegisterAsync(Sample("a-6"));

        var before = (await store.GetAsync(new AgentId("a-6"))).Value.LastHeartbeat;
        await Task.Delay(10);
        var beat = await store.UpdateHeartbeatAsync(new AgentId("a-6"));
        beat.IsSuccess.ShouldBeTrue();

        var after = (await store.GetAsync(new AgentId("a-6"))).Value.LastHeartbeat;
        after.ShouldBeGreaterThan(before);
    }

    [Fact]
    public async Task UpdateStatusAsync_UpdatesStatus()
    {
        await using var ctx = _pg.NewContext();
        var store = new EfAgentRegistrationStore(ctx);
        await store.RegisterAsync(Sample("a-7"));

        var result = await store.UpdateStatusAsync(new AgentId("a-7"), AgentStatus.Healthy);
        result.IsSuccess.ShouldBeTrue();

        (await store.GetAsync(new AgentId("a-7"))).Value.Status.ShouldBe(AgentStatus.Healthy);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllRegistered()
    {
        await using var ctx = _pg.NewContext();
        var store = new EfAgentRegistrationStore(ctx);
        await store.RegisterAsync(Sample("a-8"));
        await store.RegisterAsync(Sample("a-9"));

        var all = await store.GetAllAsync();
        all.Select(x => x.AgentId.Value).ShouldContain("a-8");
        all.Select(x => x.AgentId.Value).ShouldContain("a-9");
    }
}
