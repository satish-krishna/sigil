using Shouldly;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Core.Tests.Registry;

public class SkillTests
{
    [Fact]
    public void Defaults_AreEmpty()
    {
        var s = new Skill { Name = "summarize-pdf", Description = "x" };

        s.RequiredTools.ShouldBeEmpty();
        s.EstimatedMaxTokens.ShouldBeNull();
        s.Version.ShouldBe("1.0.0");
    }

    [Fact]
    public void TwoSkillsWithSameFields_AreEqual()
    {
        var a = new Skill
        {
            Name = "summarize-pdf",
            Description = "Summarize a PDF.",
            RequiredTools = new[] { "fetch_pdf", "extract_text" },
            EstimatedMaxTokens = 800,
            Version = "1.2.0"
        };
        var b = a with { };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void TwoSkillsDifferingInOneField_AreNotEqual()
    {
        var a = new Skill { Name = "x", Description = "y" };
        var b = a with { Name = "z" };

        a.ShouldNotBe(b);
    }

    [Fact]
    public void TwoSkills_FromIndependentConstruction_AreEqual()
    {
        var a = new Skill
        {
            Name = "summarize-pdf",
            Description = "Summarize a PDF.",
            RequiredTools = new[] { "fetch_pdf", "extract_text" },
            EstimatedMaxTokens = 800,
            Version = "1.2.0"
        };
        var b = new Skill
        {
            Name = "summarize-pdf",
            Description = "Summarize a PDF.",
            RequiredTools = new[] { "fetch_pdf", "extract_text" },
            EstimatedMaxTokens = 800,
            Version = "1.2.0"
        };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void TwoSkills_DifferingInToolOrder_AreNotEqual()
    {
        var a = new Skill { Name = "x", Description = "y", RequiredTools = new[] { "tool1", "tool2" } };
        var b = new Skill { Name = "x", Description = "y", RequiredTools = new[] { "tool2", "tool1" } };

        a.ShouldNotBe(b);
    }
}
