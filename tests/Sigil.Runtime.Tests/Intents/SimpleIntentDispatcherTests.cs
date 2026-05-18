using CSharpFunctionalExtensions;
using Shouldly;
using Sigil.Core.Gateway;
using Xunit;
using Sigil.Core.Identity;
using Sigil.Core.Intents;
using Sigil.Core.Protocol;
using Sigil.Core.Registry;
using Sigil.Runtime.Intents;
using Sigil.Runtime.Registry;
using Sigil.Runtime.Tests.Registry;

namespace Sigil.Runtime.Tests.Intents;

public sealed class SimpleIntentDispatcherTests
{
    private static AgentRegistration MakeHealthyAgent(string id = "agent-1", string skill = "echo") =>
        new()
        {
            AgentId = new AgentId(id),
            Name = id,
            Domain = "test",
            EndpointUrl = "https://localhost/agent",
            Status = AgentStatus.Healthy,
            Model = new ModelSpec { Provider = "test", Model = "test" },
            Skills = new[] { new Skill { Name = skill, Description = skill } },
        };

    private static (SimpleIntentDispatcher Dispatcher, FakeAgentRegistrationStore Store, FakeAgentGateway Gateway)
        BuildSut()
    {
        var store = new FakeAgentRegistrationStore();
        var registry = new AgentRegistry(store, new StubRandomProvider(0));
        var gateway = new FakeAgentGateway();
        return (new SimpleIntentDispatcher(registry, gateway), store, gateway);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingSkillName_ReturnsSkillNameRequired(string? skill)
    {
        var (sut, _, gw) = BuildSut();

        var result = await sut.DispatchAsync(new IntentRequest { SkillName = skill!, Input = "hi" });

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IntentErrors.SkillNameRequired);
        gw.ExecuteCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task SnapshotJobId_IsPreserved_AndAppliedToTask()
    {
        var (sut, store, gw) = BuildSut();
        await store.RegisterAsync(MakeHealthyAgent());
        gw.OnExecute = (_, _) => Result.Success(new AgentExecutionResult { Delta = new ContextDelta() });

        var jobId = new JobId("caller-job-1");
        var snapshot = new ContextSnapshot { JobId = jobId };

        var result = await sut.DispatchAsync(
            new IntentRequest { SkillName = "echo", Input = "hi", Snapshot = snapshot });

        result.IsSuccess.ShouldBeTrue();
        var call = gw.ExecuteCalls.ShouldHaveSingleItem();
        call.Package.Task.JobId.ShouldBe(jobId);
        call.Package.Snapshot.JobId.ShouldBe(jobId);
    }

    [Fact]
    public async Task NoAgentForSkill_ReturnsFailure()
    {
        var (sut, _, _) = BuildSut();

        var result = await sut.DispatchAsync(new IntentRequest { SkillName = "echo", Input = "hi" });

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IntentErrors.NoAgentForSkill);
    }

    [Fact]
    public async Task GatewayValidateFailure_PropagatesError()
    {
        var (sut, store, gw) = BuildSut();
        await store.RegisterAsync(MakeHealthyAgent());
        gw.OnValidate = (_, _) => Result.Failure<ValidationResult>("circuit-open");

        var result = await sut.DispatchAsync(new IntentRequest { SkillName = "echo", Input = "hi" });

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("circuit-open");
        gw.ExecuteCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidationRejected_WithReason_ReturnsReason()
    {
        var (sut, store, gw) = BuildSut();
        await store.RegisterAsync(MakeHealthyAgent());
        gw.OnValidate = (_, _) => Result.Success(new ValidationResult { CanHandle = false, Reason = "too-many-tokens" });

        var result = await sut.DispatchAsync(new IntentRequest { SkillName = "echo", Input = "hi" });

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("too-many-tokens");
        gw.ExecuteCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidationRejected_NoReason_ReturnsFallback()
    {
        var (sut, store, gw) = BuildSut();
        await store.RegisterAsync(MakeHealthyAgent());
        gw.OnValidate = (_, _) => Result.Success(new ValidationResult { CanHandle = false });

        var result = await sut.DispatchAsync(new IntentRequest { SkillName = "echo", Input = "hi" });

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(IntentErrors.ValidationRejected);
    }

    [Fact]
    public async Task HappyPath_CallsExecuteWithSelectedAgent()
    {
        var (sut, store, gw) = BuildSut();
        var agent = MakeHealthyAgent();
        await store.RegisterAsync(agent);

        var expected = new AgentExecutionResult { Delta = new ContextDelta() };
        gw.OnExecute = (_, _) => Result.Success(expected);

        var result = await sut.DispatchAsync(new IntentRequest { SkillName = "echo", Input = "hello" });

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
        gw.ExecuteCalls.Count.ShouldBe(1);
        gw.ExecuteCalls[0].Agent.AgentId.ShouldBe(agent.AgentId);
        gw.ExecuteCalls[0].Package.Task.SkillName.ShouldBe("echo");
        gw.ExecuteCalls[0].Package.Task.Input.ShouldBe("hello");
    }
}
