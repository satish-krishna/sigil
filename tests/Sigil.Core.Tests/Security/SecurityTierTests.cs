using System.Text.Json;
using Shouldly;
using Sigil.Core.Security;
using Xunit;

namespace Sigil.Core.Tests.Security;

public class SecurityTierTests
{
    [Fact]
    public void Default_Value_Is_Open()
    {
        default(SecurityTier).ShouldBe(SecurityTier.Open);
    }

    [Theory]
    [InlineData(SecurityTier.Open, "\"Open\"")]
    [InlineData(SecurityTier.Standard, "\"Standard\"")]
    [InlineData(SecurityTier.Trusted, "\"Trusted\"")]
    public void Serializes_As_String(SecurityTier value, string expectedJson)
    {
        JsonSerializer.Serialize(value).ShouldBe(expectedJson);
    }

    [Theory]
    [InlineData("\"Open\"", SecurityTier.Open)]
    [InlineData("\"Standard\"", SecurityTier.Standard)]
    [InlineData("\"Trusted\"", SecurityTier.Trusted)]
    public void Deserializes_From_String(string json, SecurityTier expected)
    {
        JsonSerializer.Deserialize<SecurityTier>(json).ShouldBe(expected);
    }
}
