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

    [Fact]
    public async Task MissingKey_Returns_MissingKey()
    {
        var validator = MakeValidator(OpenWithKey("echo-agent", "dev-key-echo"));
        var creds = new SigilCredentials { AgentId = new AgentId("echo-agent"), SigilKey = null };

        var result = await validator.AuthenticateAsync(creds, SecurityTier.Open);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilSecurityErrors.MissingKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task WhitespaceKey_Returns_MissingKey(string presented)
    {
        var validator = MakeValidator(OpenWithKey("echo-agent", "dev-key-echo"));
        var creds = new SigilCredentials { AgentId = new AgentId("echo-agent"), SigilKey = presented };

        var result = await validator.AuthenticateAsync(creds, SecurityTier.Open);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilSecurityErrors.MissingKey);
    }

    [Fact]
    public async Task UnknownAgent_Returns_UnknownAgent()
    {
        var validator = MakeValidator(OpenWithKey("echo-agent", "dev-key-echo"));
        var creds = new SigilCredentials
        {
            AgentId = new AgentId("research-agent"),
            SigilKey = "dev-key-echo"
        };

        var result = await validator.AuthenticateAsync(creds, SecurityTier.Open);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilSecurityErrors.UnknownAgent);
    }

    [Fact]
    public async Task WrongKey_Returns_KeyMismatch()
    {
        var validator = MakeValidator(OpenWithKey("echo-agent", "dev-key-echo"));
        var creds = new SigilCredentials
        {
            AgentId = new AgentId("echo-agent"),
            SigilKey = "WRONG"
        };

        var result = await validator.AuthenticateAsync(creds, SecurityTier.Open);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilSecurityErrors.KeyMismatch);
    }

    [Fact]
    public async Task ModeMisconfigured_Returns_ModeMismatch()
    {
        var opts = new SigilSecurityOptions { Mode = SecurityTier.Standard };
        opts.OpenTier.Keys["echo-agent"] = "dev-key-echo";
        var validator = MakeValidator(opts);
        var creds = new SigilCredentials
        {
            AgentId = new AgentId("echo-agent"),
            SigilKey = "dev-key-echo"
        };

        var result = await validator.AuthenticateAsync(creds, SecurityTier.Open);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilSecurityErrors.ModeMismatch);
    }

    [Theory]
    [InlineData(SecurityTier.Standard)]
    [InlineData(SecurityTier.Trusted)]
    public async Task TierEscalationAboveOpen_Returns_TierNotSupported(SecurityTier requiredTier)
    {
        var validator = MakeValidator(OpenWithKey("echo-agent", "dev-key-echo"));
        var creds = new SigilCredentials
        {
            AgentId = new AgentId("echo-agent"),
            SigilKey = "dev-key-echo"
        };

        var result = await validator.AuthenticateAsync(creds, requiredTier);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilSecurityErrors.TierNotSupported);
    }
}
