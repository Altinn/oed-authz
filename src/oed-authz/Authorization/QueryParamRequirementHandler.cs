using Microsoft.AspNetCore.Authorization;

namespace oed_authz.Authorization;

public class QueryParamRequirementHandler : AuthorizationHandler<QueryParamRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public QueryParamRequirementHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, QueryParamRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is not null && httpContext.Request.Query.TryGetValue(requirement.QueryParamName, out var queryValue))
        {
            if (queryValue == requirement.Secret)
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }
        }
        else
        {
            context.Fail();
        }

        return Task.CompletedTask;
    }
}
