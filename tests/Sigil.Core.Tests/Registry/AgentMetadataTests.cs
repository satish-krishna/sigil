using Shouldly;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Core.Tests.Registry;

public class AgentMetadataTests
{
    [Fact]
    public void Default_HasEmptyTags()
    {
        var m = new AgentMetadata();

        m.Tags.ShouldBeEmpty();
    }

    [Fact]
    public void TwoMetadataWithSameTags_AreEqual()
    {
        var a = new AgentMetadata
        {
            Tags = new Dictionary<string, string> { ["team"] = "platform" }
        };
        var b = a with { };

        a.ShouldBe(b);
    }
}
