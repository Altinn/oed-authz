namespace oed_authz.Models;

public class ProxyManagementRequest
{
    public string EstateSsn { get; set; } = string.Empty;
    public ProxyRoleAssignment ProxyRoleAssignment { get; set; } = new();
}
