using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Security;
using Sigil.Infrastructure.Security;
using Xunit;

namespace Sigil.Infrastructure.Tests.Security;

public class AddSigilSecurityTests
{
    private static IConfiguration BuildConfig(IDictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    // The test project doesn't reference Microsoft.Extensions.Logging (the package that
    // hosts AddLogging()), so register the open-generic ILogger<> -> NullLogger<>
    // directly. ILogger<T> and NullLogger<T> both come transitively via
    // Microsoft.Extensions.Logging.Abstractions.
    private static IServiceCollection NewServices()
        => new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    [Fact]
    public void Resolves_ISigilSecurity_As_SigilKeyValidator()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Security:Mode"] = "Open",
            ["Security:OpenTier:Keys:echo-agent"] = "dev-key-echo"
        });

        using var provider = NewServices()
            .AddSigilSecurity(config)
            .BuildServiceProvider();

        var resolved = provider.GetRequiredService<ISigilSecurity>();

        resolved.ShouldBeOfType<SigilKeyValidator>();
    }

    [Fact]
    public void Options_Bind_From_Configuration()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Security:Mode"] = "Open",
            ["Security:OpenTier:Keys:echo-agent"] = "dev-key-echo",
            ["Security:OpenTier:Keys:research-agent"] = "dev-key-research"
        });

        using var provider = NewServices()
            .AddSigilSecurity(config)
            .BuildServiceProvider();

        var opts = provider.GetRequiredService<IOptionsMonitor<SigilSecurityOptions>>().CurrentValue;

        opts.Mode.ShouldBe(SecurityTier.Open);
        opts.OpenTier.Keys["echo-agent"].ShouldBe("dev-key-echo");
        opts.OpenTier.Keys["research-agent"].ShouldBe("dev-key-research");
    }

    [Fact]
    public async Task Validator_Authenticates_Against_Bound_Allowlist()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Security:Mode"] = "Open",
            ["Security:OpenTier:Keys:echo-agent"] = "dev-key-echo"
        });

        using var provider = NewServices()
            .AddSigilSecurity(config)
            .BuildServiceProvider();

        var sec = provider.GetRequiredService<ISigilSecurity>();
        var creds = new SigilCredentials
        {
            AgentId = new AgentId("echo-agent"),
            SigilKey = "dev-key-echo"
        };

        var result = await sec.AuthenticateAsync(creds, SecurityTier.Open);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AgentId.ShouldBe(new AgentId("echo-agent"));
    }
}
