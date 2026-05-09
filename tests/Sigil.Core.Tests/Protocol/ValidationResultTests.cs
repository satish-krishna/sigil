using Shouldly;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class ValidationResultTests
{
    [Fact]
    public void Defaults_AreFalseAndEmpty()
    {
        var r = new ValidationResult();

        r.CanHandle.ShouldBeFalse();
        r.EstimatedTokens.ShouldBeNull();
        r.MissingTools.ShouldBeEmpty();
        r.Reason.ShouldBeNull();
    }

    [Fact]
    public void TwoResults_FromIndependentConstruction_AreEqual()
    {
        var a = new ValidationResult
        {
            CanHandle = false,
            EstimatedTokens = 1_200,
            MissingTools = new[] { "fetch_pdf" },
            Reason = "tool not available"
        };
        var b = new ValidationResult
        {
            CanHandle = false,
            EstimatedTokens = 1_200,
            MissingTools = new[] { "fetch_pdf" },
            Reason = "tool not available"
        };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void TwoResults_DifferingInOneField_AreNotEqual()
    {
        var a = new ValidationResult { CanHandle = true };
        var b = a with { CanHandle = false };

        a.ShouldNotBe(b);
    }

    [Fact]
    public void TwoResults_DifferingInToolOrder_AreNotEqual()
    {
        var a = new ValidationResult { MissingTools = new[] { "t1", "t2" } };
        var b = new ValidationResult { MissingTools = new[] { "t2", "t1" } };

        a.ShouldNotBe(b);
    }
}
