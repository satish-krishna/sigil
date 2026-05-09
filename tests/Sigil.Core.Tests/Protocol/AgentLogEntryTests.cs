using Shouldly;
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

        entry.Level.ShouldBe("Info");
        entry.Message.ShouldBe("");
        entry.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
        entry.Timestamp.ShouldBeLessThanOrEqualTo(after);
        entry.Timestamp.Kind.ShouldBe(DateTimeKind.Utc);
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

        entry.Timestamp.ShouldBe(ts);
        entry.AgentId.ShouldBe(new AgentId("agent-1"));
        entry.Level.ShouldBe("Warn");
        entry.Message.ShouldBe("hello");
    }
}
