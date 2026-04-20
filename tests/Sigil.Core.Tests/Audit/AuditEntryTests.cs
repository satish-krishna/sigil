using FluentAssertions;
using Sigil.Core.Audit;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Audit;

public class AuditEntryTests
{
    [Fact]
    public void Default_GeneratesAuditIdAndUtcTimestamp()
    {
        var before = DateTime.UtcNow;
        var entry = new AuditEntry();
        var after = DateTime.UtcNow;

        entry.AuditId.Should().NotBeNullOrWhiteSpace();
        entry.AuditId.Length.Should().Be(32); // Guid "N" format
        entry.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        entry.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void TwoDefaultEntries_HaveDifferentAuditIds()
    {
        var a = new AuditEntry();
        var b = new AuditEntry();

        a.AuditId.Should().NotBe(b.AuditId);
    }

    [Fact]
    public void TwoEntriesWithIdenticalFields_AreEqual()
    {
        var delta = new ContextDelta();
        var metrics = new UsageMetrics();

        var a = new AuditEntry
        {
            AuditId = "fixed-id",
            JobId = new JobId("j"),
            AgentId = new AgentId("a"),
            StepId = new StepId("s"),
            Delta = delta,
            Metrics = metrics,
            Timestamp = new DateTime(2026, 4, 19, 0, 0, 0, DateTimeKind.Utc)
        };
        var b = a with { };

        a.Should().Be(b);
    }

    [Fact]
    public void TwoEntriesDifferingInOneField_AreNotEqual()
    {
        var a = new AuditEntry { AuditId = "x", JobId = new JobId("j1") };
        var b = a with { JobId = new JobId("j2") };

        a.Should().NotBe(b);
    }
}
