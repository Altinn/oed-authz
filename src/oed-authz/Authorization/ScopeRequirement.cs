using Microsoft.AspNetCore.Authorization;

namespace oed_authz.Authorization;

public class ScopeRequirement : IAuthorizationRequirement
{
    public string[] Scope { get; }

    public ScopeRequirement(string scope)
    {
        Scope = new [] { scope };
    }

    public ScopeRequirement(string[] scopes)
    {
        Scope = scopes;
    }
}
