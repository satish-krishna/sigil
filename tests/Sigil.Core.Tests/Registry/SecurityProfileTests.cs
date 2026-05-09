using Shouldly;
using Sigil.Core.Registry;
using Sigil.Core.Security;
using Xunit;

namespace Sigil.Core.Tests.Registry;

public class SecurityProfileTests
{
    [Fact]
    public void Defaults_HaveEmptyAllowedToolsAndNoSecrets()
    {
        var s = new SecurityProfile();

        s.CertificateThumbprint.ShouldBeNull();
        s.SigilKey.ShouldBeNull();
        s.IsPiiCleared.ShouldBeFalse();
        s.AllowedTools.ShouldBeEmpty();
    }

    [Fact]
    public void Default_Tier_Is_Open()
    {
        new SecurityProfile().Tier.ShouldBe(SecurityTier.Open);
    }

    [Fact]
    public void TwoProfiles_FromIndependentConstruction_AreEqual()
    {
        var a = new SecurityProfile
        {
            CertificateThumbprint = "abc",
            SigilKey = "key",
            IsPiiCleared = true,
            AllowedTools = new[] { "tool1", "tool2" },
            Tier = SecurityTier.Trusted
        };
        var b = new SecurityProfile
        {
            CertificateThumbprint = "abc",
            SigilKey = "key",
            IsPiiCleared = true,
            AllowedTools = new[] { "tool1", "tool2" },
            Tier = SecurityTier.Trusted
        };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void TwoProfiles_DifferingInAllowedTools_AreNotEqual()
    {
        var a = new SecurityProfile { AllowedTools = new[] { "tool1" } };
        var b = new SecurityProfile { AllowedTools = new[] { "tool2" } };

        a.ShouldNotBe(b);
    }

    [Fact]
    public void TwoProfiles_DifferingOnlyInTier_AreNotEqual()
    {
        var a = new SecurityProfile { Tier = SecurityTier.Open };
        var b = new SecurityProfile { Tier = SecurityTier.Standard };

        a.ShouldNotBe(b);
    }
}
