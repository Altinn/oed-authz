namespace oed_authz.Models;
public class PipRoleAssignment
{
    public long Id { get; set; }

    public string RoleCode { get; init; } = string.Empty;

    public string EstateSsn { get; init; } = string.Empty;

    public string? HeirSsn { get; init; }

    public string RecipientSsn { get; init; } = string.Empty;

    public DateTimeOffset Created { get; set; }
    public bool IsRestricted { get; set; }
}
