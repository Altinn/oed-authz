namespace oed_authz.Models;

public class ProxyRoleAssignment
{
    public string HeirSsn { get; init; } = string.Empty;
    public string RecipientSsn { get; init; } = string.Empty;
    public string RoleCode { get; init; } = string.Empty;
    public DateTimeOffset? Created { get; init; } = DateTimeOffset.Now;
}
