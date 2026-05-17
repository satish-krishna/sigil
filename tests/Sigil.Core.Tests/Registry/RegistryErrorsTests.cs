using Shouldly;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Core.Tests.Registry;

public class RegistryErrorsTests
{
    [Fact]
    public void Constants_have_stable_string_values()
    {
        RegistryErrors.AgentNotFound.ShouldBe("agent-not-found");
        RegistryErrors.InvalidStatusTransition.ShouldBe("invalid-status-transition");
        RegistryErrors.InvalidRoutingWeight.ShouldBe("invalid-routing-weight");
        RegistryErrors.SkillNameRequired.ShouldBe("skill-name-required");
    }
}
