using Shouldly;
using Sigil.Core.Security;
using Xunit;

namespace Sigil.Core.Tests.Security;

public class SigilSecurityErrorsTests
{
    [Fact]
    public void ErrorCodes_HaveStableValues()
    {
        SigilSecurityErrors.MissingKey.ShouldBe("missing-key");
        SigilSecurityErrors.UnknownAgent.ShouldBe("unknown-agent");
        SigilSecurityErrors.KeyMismatch.ShouldBe("key-mismatch");
        SigilSecurityErrors.TierNotSupported.ShouldBe("tier-not-supported");
        SigilSecurityErrors.ModeMismatch.ShouldBe("mode-mismatch");
    }
}
