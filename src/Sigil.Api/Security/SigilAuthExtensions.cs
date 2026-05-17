using Sigil.Core.Identity;
using Sigil.Core.Security;

namespace Sigil.Api.Security;

public static class SigilAuthExtensions
{
    public static AgentId GetAuthenticatedAgentId(this HttpContext ctx)
    {
        var auth = (AuthenticationResult?)ctx.Items[SigilAuthMiddleware.AuthContextItemKey]
            ?? throw new InvalidOperationException(
                "SigilAuthMiddleware did not run or did not authenticate this request.");
        return auth.AgentId;
    }
}
