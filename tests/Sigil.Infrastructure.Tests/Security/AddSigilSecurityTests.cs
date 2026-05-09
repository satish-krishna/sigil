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

    [Fact]
    public void EmptyAllowlist_Passes_ValidateOnStart()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Security:Mode"] = "Open"
            // no Keys
        });

        using var provider = NewServices()
            .AddSigilSecurity(config)
            .BuildServiceProvider();

        // ValidateOnStart triggers on first IOptions resolution.
        var opts = provider.GetRequiredService<IOptionsMonitor<SigilSecurityOptions>>().CurrentValue;

        opts.OpenTier.Keys.ShouldBeEmpty();
    }

    [Fact]
    public async Task Validator_PicksUp_OptionsMonitor_Reload()
    {
        var initial = new SigilSecurityOptions { Mode = SecurityTier.Open };
        initial.OpenTier.Keys["echo-agent"] = "old-key";

        var monitor = new TestOptionsMonitor<SigilSecurityOptions>(initial);
        var validator = new SigilKeyValidator(monitor, NullLogger<SigilKeyValidator>.Instance);

        var creds = new SigilCredentials
        {
            AgentId = new AgentId("echo-agent"),
            SigilKey = "new-key"
        };

        // Before reload: presented key doesn't match the configured "old-key".
        var beforeReload = await validator.AuthenticateAsync(creds, SecurityTier.Open);
        beforeReload.IsFailure.ShouldBeTrue();
        beforeReload.Error.ShouldBe(SigilSecurityErrors.KeyMismatch);

        // Reload: rotate the configured key to match what the agent will present.
        var rotated = new SigilSecurityOptions { Mode = SecurityTier.Open };
        rotated.OpenTier.Keys["echo-agent"] = "new-key";
        monitor.CurrentValue = rotated;

        // After reload: same call, different outcome.
        var afterReload = await validator.AuthenticateAsync(creds, SecurityTier.Open);
        afterReload.IsSuccess.ShouldBeTrue();
        afterReload.Value.AgentId.ShouldBe(new AgentId("echo-agent"));
    }
}
