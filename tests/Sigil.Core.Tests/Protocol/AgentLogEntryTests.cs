using FluentAssertions;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class AgentLogEntryTests
{
    [Fact]
    public void Default_HasInfoLevelAndEmptyMessageAndUtcTimestamp()
    {
        var before = DateTime.UtcNow;
        var entry = new AgentLogEntry();
        var after = DateTime.UtcNow;

        entry.Level.Should().Be("Info");
        entry.Message.Should().Be("");
        entry.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        entry.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void InitProperties_Roundtrip()
    {
        var ts = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);
        var entry = new AgentLogEntry
        {
            Timestamp = ts,
            AgentId = new AgentId("agent-1"),
            Level = "Warn",
            Message = "hello"
        };

        entry.Timestamp.Should().Be(ts);
        entry.AgentId.Should().Be(new AgentId("agent-1"));
        entry.Level.Should().Be("Warn");
        entry.Message.Should().Be("hello");
    }
}
