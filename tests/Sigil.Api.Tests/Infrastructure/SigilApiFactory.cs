using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sigil.Core.Gateway;
using Sigil.Core.Storage;

namespace Sigil.Api.Tests.Infrastructure;

public sealed class SigilApiFactory : WebApplicationFactory<Program>
{
    public FakeAgentRegistrationStore Store { get; } = new();
    public StubAgentGateway Gateway { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:Mode"] = "Open",
                [$"Security:OpenTier:Keys:{TestKeys.AgentA}"] = TestKeys.AgentAKey,
                [$"Security:OpenTier:Keys:{TestKeys.AgentB}"] = TestKeys.AgentBKey,
                ["Storage:EfCore:ConnectionString"] = "Host=ignored;Database=ignored;Username=ignored;Password=ignored",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(IAgentRegistrationStore));
            services.AddSingleton<IAgentRegistrationStore>(Store);

            // IAgentGateway is registered in Program.cs via AddAgentGateway; RemoveAll
            // replaces the production registration with the test stub.
            services.RemoveAll(typeof(IAgentGateway));
            services.AddSingleton<IAgentGateway>(Gateway);
        });
    }

    public HttpClient CreateAuthedClient(string agentId, string key)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Sigil-Agent-Id", agentId);
        client.DefaultRequestHeaders.Add("X-Sigil-Key", key);
        return client;
    }
}
