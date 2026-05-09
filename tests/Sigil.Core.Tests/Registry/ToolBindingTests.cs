using Shouldly;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Core.Tests.Registry;

public class ToolBindingTests
{
    [Fact]
    public void ToolKind_HasExpectedMembers()
    {
        Enum.GetValues<ToolKind>().ShouldBe(
            new[] { ToolKind.Mcp, ToolKind.Http, ToolKind.InProcess },
            ignoreOrder: true);
    }

    [Fact]
    public void TwoToolBindingsWithSameFields_AreEqual()
    {
        var a = new ToolBinding
        {
            Name = "get_forecast",
            Kind = ToolKind.Http,
            Description = "Fetch a 7-day forecast.",
            ParameterSchema = "{\"type\":\"object\"}"
        };
        var b = a with { };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void TwoToolBindingsDifferingInKind_AreNotEqual()
    {
        var a = new ToolBinding
        {
            Name = "get_forecast",
            Kind = ToolKind.Http,
            Description = "x",
            ParameterSchema = "{}"
        };
        var b = a with { Kind = ToolKind.Mcp };

        a.ShouldNotBe(b);
    }
}
