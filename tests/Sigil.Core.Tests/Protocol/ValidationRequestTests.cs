using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class ValidationRequestTests
{
    private static AgentTask SampleTask() => new()
    {
        JobId = new JobId("job-1"),
        StepId = new StepId("step-1"),
        SkillName = "summarize-pdf",
        AvailableTools = new[] { "fetch_pdf" }
    };

    [Fact]
    public void Defaults_AreZero()
    {
        var r = new ValidationRequest { Task = SampleTask() };

        r.AvailableTokenBudget.ShouldBe(0);
    }

    [Fact]
    public void TwoRequests_FromIndependentConstruction_AreEqual()
    {
        var a = new ValidationRequest { Task = SampleTask(), AvailableTokenBudget = 4_000 };
        var b = new ValidationRequest { Task = SampleTask(), AvailableTokenBudget = 4_000 };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void TwoRequests_DifferingInBudget_AreNotEqual()
    {
        var a = new ValidationRequest { Task = SampleTask(), AvailableTokenBudget = 4_000 };
        var b = a with { AvailableTokenBudget = 8_000 };

        a.ShouldNotBe(b);
    }
}
