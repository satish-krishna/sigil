using Shouldly;
using Sigil.Core.Security;
using Sigil.Infrastructure.Security;
using Xunit;

namespace Sigil.Infrastructure.Tests.Security;

public class SigilSecurityOptionsTests
{
    [Fact]
    public void Defaults_AreOpenModeAndEmptyAllowlist()
    {
        var opts = new SigilSecurityOptions();

        opts.Mode.ShouldBe(SecurityTier.Open);
        opts.OpenTier.ShouldNotBeNull();
        opts.OpenTier.Keys.ShouldBeEmpty();
    }

    [Fact]
    public void OpenTier_KeyComparison_IsOrdinalAndCaseSensitive()
    {
        var opts = new SigilSecurityOptions();
        opts.OpenTier.Keys["Echo-Agent"] = "k";

        opts.OpenTier.Keys.ContainsKey("echo-agent").ShouldBeFalse();
        opts.OpenTier.Keys.ContainsKey("Echo-Agent").ShouldBeTrue();
    }

    [Fact]
    public void SectionName_Is_Security()
    {
        SigilSecurityOptions.SectionName.ShouldBe("Security");
    }
}
