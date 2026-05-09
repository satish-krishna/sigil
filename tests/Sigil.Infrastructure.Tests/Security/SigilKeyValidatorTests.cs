using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Security;
using Sigil.Infrastructure.Security;
using Xunit;

namespace Sigil.Infrastructure.Tests.Security;

public class SigilKeyValidatorTests
{
    private static SigilKeyValidator MakeValidator(SigilSecurityOptions options)
        => new(new TestOptionsMonitor<SigilSecurityOptions>(options),
               NullLogger<SigilKeyValidator>.Instance);

    private static SigilSecurityOptions OpenWithKey(string agentId, string key)
    {
        var o = new SigilSecurityOptions { Mode = SecurityTier.Open };
        o.OpenTier.Keys[agentId] = key;
        return o;
    }

    [Fact]
    public async Task CorrectKey_ReturnsSuccess_WithEchoedAgentIdAndOpenTier()
    {
        var validator = MakeValidator(OpenWithKey("echo-agent", "dev-key-echo"));
        var creds = new SigilCredentials
        {
            AgentId = new AgentId("echo-agent"),
            SigilKey = "dev-key-echo"
        };

        var result = await validator.AuthenticateAsync(creds, SecurityTier.Open);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AgentId.ShouldBe(new AgentId("echo-agent"));
        result.Value.Tier.ShouldBe(SecurityTier.Open);
    }
}
