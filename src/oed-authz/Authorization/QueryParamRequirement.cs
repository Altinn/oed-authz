using Microsoft.AspNetCore.Authorization;

namespace oed_authz.Authorization;

public class QueryParamRequirement : IAuthorizationRequirement
{
    public string Secret { get; }

    public string QueryParamName { get; }

    public QueryParamRequirement(string queryParamName, string secret)
    {
        QueryParamName = queryParamName;
        Secret = secret;
    }
}
