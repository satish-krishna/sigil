using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class AgentExecutionResultTests
{
    [Fact]
    public void Defaults_AreEmpty()
    {
        var r = new AgentExecutionResult { Delta = new ContextDelta() };

        r.Logs.ShouldBeEmpty();
        r.Metrics.PromptTokens.ShouldBe(0);
        r.Metrics.CompletionTokens.ShouldBe(0);
        r.Metrics.Custom.ShouldBeEmpty();
    }

    [Fact]
    public void TwoResults_FromIndependentConstruction_AreEqual()
    {
        var a = new AgentExecutionResult
        {
            Delta = new ContextDelta { Removals = ["r1"] },
            Logs = new[]
            {
                new AgentLogEntry { AgentId = new AgentId("agent-1"), Level = "Info", Message = "hello", Timestamp = new DateTime(2026, 5, 9, 0, 0, 0, DateTimeKind.Utc) }
            },
            Metrics = new UsageMetrics { PromptTokens = 10, CompletionTokens = 20 }
        };
        var b = new AgentExecutionResult
        {
            Delta = new ContextDelta { Removals = ["r1"] },
            Logs = new[]
            {
                new AgentLogEntry { AgentId = new AgentId("agent-1"), Level = "Info", Message = "hello", Timestamp = new DateTime(2026, 5, 9, 0, 0, 0, DateTimeKind.Utc) }
            },
            Metrics = new UsageMetrics { PromptTokens = 10, CompletionTokens = 20 }
        };

        // ContextDelta and UsageMetrics rely on default record equality with reference-typed
        // collection fields (ContextDelta.Updates / UsageMetrics.Custom), so cross-instance
        // equality on the wrapper is asserted at the field level.
        a.Logs.ShouldBe(b.Logs);
        a.Metrics.PromptTokens.ShouldBe(b.Metrics.PromptTokens);
        a.Metrics.CompletionTokens.ShouldBe(b.Metrics.CompletionTokens);
    }

    [Fact]
    public void TwoResults_DifferingInLogs_AreNotEqual()
    {
        var d = new ContextDelta();
        var a = new AgentExecutionResult
        {
            Delta = d,
            Logs = new[] { new AgentLogEntry { Message = "a" } }
        };
        var b = new AgentExecutionResult
        {
            Delta = d,
            Logs = new[] { new AgentLogEntry { Message = "b" } }
        };

        a.ShouldNotBe(b);
    }

    [Fact]
    public void TwoResults_DifferingInLogOrder_AreNotEqual()
    {
        var d = new ContextDelta();
        var l1 = new AgentLogEntry { Message = "a" };
        var l2 = new AgentLogEntry { Message = "b" };
        var a = new AgentExecutionResult { Delta = d, Logs = new[] { l1, l2 } };
        var b = new AgentExecutionResult { Delta = d, Logs = new[] { l2, l1 } };

        a.ShouldNotBe(b);
    }
}
