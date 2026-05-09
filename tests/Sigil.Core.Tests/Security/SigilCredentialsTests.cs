using System.Text.Json;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Security;
using Xunit;

namespace Sigil.Core.Tests.Security;

public class SigilCredentialsTests
{
    [Fact]
    public void Defaults_HaveNullOptionalFields()
    {
        var c = new SigilCredentials { AgentId = new AgentId("agent-1") };

        c.AgentId.ShouldBe(new AgentId("agent-1"));
        c.SigilKey.ShouldBeNull();
        c.Jwt.ShouldBeNull();
        c.CertificateThumbprint.ShouldBeNull();
    }

    [Fact]
    public void TwoCredentials_FromIndependentConstruction_AreEqual()
    {
        var a = new SigilCredentials
        {
            AgentId = new AgentId("a"),
            SigilKey = "k",
            Jwt = "j",
            CertificateThumbprint = "t"
        };
        var b = new SigilCredentials
        {
            AgentId = new AgentId("a"),
            SigilKey = "k",
            Jwt = "j",
            CertificateThumbprint = "t"
        };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void TwoCredentials_DifferingInSigilKey_AreNotEqual()
    {
        var a = new SigilCredentials { AgentId = new AgentId("a"), SigilKey = "k1" };
        var b = new SigilCredentials { AgentId = new AgentId("a"), SigilKey = "k2" };

        a.ShouldNotBe(b);
    }

    [Fact]
    public void RoundTrip_Json_PreservesAllFields()
    {
        var c = new SigilCredentials
        {
            AgentId = new AgentId("agent-1"),
            SigilKey = "secret",
            Jwt = null,
            CertificateThumbprint = null
        };

        var json = JsonSerializer.Serialize(c);
        var back = JsonSerializer.Deserialize<SigilCredentials>(json);

        back.ShouldBe(c);
    }
}
