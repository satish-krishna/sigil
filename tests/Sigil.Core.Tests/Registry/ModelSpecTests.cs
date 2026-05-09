using Shouldly;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Core.Tests.Registry;

public class ModelSpecTests
{
    [Fact]
    public void Sampling_Defaults_AreNull()
    {
        var s = new Sampling();

        s.Temperature.ShouldBeNull();
        s.TopP.ShouldBeNull();
        s.MaxOutputTokens.ShouldBeNull();
    }

    [Fact]
    public void ModelSpec_Defaults_HasEmptySampling()
    {
        var m = new ModelSpec { Provider = "openai", Model = "gpt-4o-mini" };

        m.Sampling.ShouldNotBeNull();
        m.Sampling.Temperature.ShouldBeNull();
    }

    [Fact]
    public void TwoModelSpecsWithSameFields_AreEqual()
    {
        var a = new ModelSpec
        {
            Provider = "openai",
            Model = "gpt-4o-mini",
            Sampling = new Sampling { Temperature = 0.2, MaxOutputTokens = 800 }
        };
        var b = a with { };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void TwoModelSpecsDifferingInSampling_AreNotEqual()
    {
        var a = new ModelSpec
        {
            Provider = "openai",
            Model = "gpt-4o-mini",
            Sampling = new Sampling { Temperature = 0.2 }
        };
        var b = a with { Sampling = new Sampling { Temperature = 0.7 } };

        a.ShouldNotBe(b);
    }
}
