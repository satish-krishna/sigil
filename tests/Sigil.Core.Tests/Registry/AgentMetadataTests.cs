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

    [Fact]
    public void TwoMetadata_FromIndependentConstruction_AreEqual()
    {
        var a = new AgentMetadata
        {
            Tags = new Dictionary<string, string> { ["team"] = "platform", ["tier"] = "standard" }
        };
        var b = new AgentMetadata
        {
            Tags = new Dictionary<string, string> { ["team"] = "platform", ["tier"] = "standard" }
        };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void TwoMetadata_WithSameKeysInDifferentOrder_AreEqual()
    {
        var a = new AgentMetadata
        {
            Tags = new Dictionary<string, string> { ["team"] = "platform", ["tier"] = "standard" }
        };
        var b = new AgentMetadata
        {
            Tags = new Dictionary<string, string> { ["tier"] = "standard", ["team"] = "platform" }
        };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void TwoMetadata_DifferingInValue_AreNotEqual()
    {
        var a = new AgentMetadata
        {
            Tags = new Dictionary<string, string> { ["team"] = "platform" }
        };
        var b = new AgentMetadata
        {
            Tags = new Dictionary<string, string> { ["team"] = "infrastructure" }
        };

        a.ShouldNotBe(b);
    }
}
