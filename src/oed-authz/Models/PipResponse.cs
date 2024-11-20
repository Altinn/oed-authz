namespace oed_authz.Models;

public class PipResponse
{
    public string EstateSsn { get; set; } = string.Empty;
    public List<PipRoleAssignment> RoleAssignments { get; set; } = new();
}
