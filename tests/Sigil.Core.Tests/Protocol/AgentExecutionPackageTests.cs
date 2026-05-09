using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class AgentExecutionPackageTests
{
    private static AgentTask SampleTask() => new()
    {
        JobId = new JobId("job-1"),
        StepId = new StepId("step-1"),
        SkillName = "summarize-pdf",
    };

    private static ContextSnapshot SampleSnapshot() => new()
    {
        JobId = new JobId("job-1"),
        State = new Dictionary<string, object> { ["k"] = "v" }
    };

    [Fact]
    public void Constructs_WithRequiredFields()
    {
        var pkg = new AgentExecutionPackage
        {
            Task = SampleTask(),
            Snapshot = SampleSnapshot(),
            ExpectedETag = new ETag("etag-1")
        };

        pkg.Task.SkillName.ShouldBe("summarize-pdf");
        pkg.Snapshot.JobId.ShouldBe(new JobId("job-1"));
        pkg.ExpectedETag.ShouldBe(new ETag("etag-1"));
    }

    [Fact]
    public void TwoPackages_FromIndependentConstruction_AreEqual_OnTaskAndETag()
    {
        var a = new AgentExecutionPackage
        {
            Task = SampleTask(),
            Snapshot = SampleSnapshot(),
            ExpectedETag = new ETag("etag-1")
        };
        var b = new AgentExecutionPackage
        {
            Task = SampleTask(),
            Snapshot = SampleSnapshot(),
            ExpectedETag = new ETag("etag-1")
        };

        // Snapshot is reference-typed and not value-equal; AgentExecutionPackage
        // intentionally uses default record equality. We assert on field-level.
        a.Task.ShouldBe(b.Task);
        a.ExpectedETag.ShouldBe(b.ExpectedETag);
    }

    [Fact]
    public void TwoPackagesDifferingInETag_AreNotEqual()
    {
        var a = new AgentExecutionPackage
        {
            Task = SampleTask(),
            Snapshot = SampleSnapshot(),
            ExpectedETag = new ETag("etag-1")
        };
        var b = a with { ExpectedETag = new ETag("etag-2") };

        a.ShouldNotBe(b);
    }
}
