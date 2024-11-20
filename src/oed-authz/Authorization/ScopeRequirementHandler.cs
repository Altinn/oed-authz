using Microsoft.AspNetCore.Authorization;

namespace oed_authz.Authorization;

public class ScopeRequirementHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        var contextScope = context.User.Claims.Where(c => c.Type.Equals("scope")).Select(c => c.Value).FirstOrDefault();
        var validScope = false;

        if (contextScope is not null)
        {
            var requiredScopes = requirement.Scope;
            var clientScopes = contextScope.Split(' ').ToList();

            if (requiredScopes.Any(requiredScope => clientScopes.Contains(requiredScope)))
            {
                validScope = true;
            }
        }

        if (validScope)
        {
            context.Succeed(requirement);
        }

        await Task.CompletedTask;
    }
}
