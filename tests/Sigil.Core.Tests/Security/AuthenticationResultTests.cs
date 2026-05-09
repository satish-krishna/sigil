using System.Text.Json;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Security;
using Xunit;

namespace Sigil.Core.Tests.Security;

public class AuthenticationResultTests
{
    [Fact]
    public void Equality_IsValueBased()
    {
        var a = new AuthenticationResult { AgentId = new AgentId("a"), Tier = SecurityTier.Open };
        var b = new AuthenticationResult { AgentId = new AgentId("a"), Tier = SecurityTier.Open };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void TwoResults_DifferingInTier_AreNotEqual()
    {
        var a = new AuthenticationResult { AgentId = new AgentId("a"), Tier = SecurityTier.Open };
        var b = a with { Tier = SecurityTier.Standard };

        a.ShouldNotBe(b);
    }

    [Fact]
    public void RoundTrip_Json_PreservesValues()
    {
        var ar = new AuthenticationResult
        {
            AgentId = new AgentId("agent-1"),
            Tier = SecurityTier.Trusted
        };

        var json = JsonSerializer.Serialize(ar);
        json.ShouldContain("\"agent-1\"");
        json.ShouldContain("\"Trusted\"");
        var back = JsonSerializer.Deserialize<AuthenticationResult>(json);

        back.ShouldBe(ar);
    }
}
