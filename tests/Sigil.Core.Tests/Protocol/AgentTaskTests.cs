using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class AgentTaskTests
{
    [Fact]
    public void Defaults_AreEmpty()
    {
        var t = new AgentTask { SkillName = "summarize-pdf" };

        t.JobId.ShouldBe(default);
        t.StepId.ShouldBe(default);
        t.Input.ShouldBe("");
        t.AvailableTools.ShouldBeEmpty();
    }

    [Fact]
    public void TwoTasks_FromIndependentConstruction_AreEqual()
    {
        var a = new AgentTask
        {
            JobId = new JobId("job-1"),
            StepId = new StepId("step-1"),
            SkillName = "summarize-pdf",
            Input = "https://example.com/doc.pdf",
            AvailableTools = new[] { "fetch_pdf", "extract_text" }
        };
        var b = new AgentTask
        {
            JobId = new JobId("job-1"),
            StepId = new StepId("step-1"),
            SkillName = "summarize-pdf",
            Input = "https://example.com/doc.pdf",
            AvailableTools = new[] { "fetch_pdf", "extract_text" }
        };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void TwoTasksDifferingInOneField_AreNotEqual()
    {
        var a = new AgentTask { SkillName = "x" };
        var b = a with { SkillName = "y" };

        a.ShouldNotBe(b);
    }

    [Fact]
    public void TwoTasks_DifferingInToolOrder_AreNotEqual()
    {
        var a = new AgentTask { SkillName = "x", AvailableTools = new[] { "t1", "t2" } };
        var b = new AgentTask { SkillName = "x", AvailableTools = new[] { "t2", "t1" } };

        a.ShouldNotBe(b);
    }
}
