namespace oed_authz.Models.Dto;

public class RoleAssignmentDto
{
    public string EstateSsn { get; set; } = null!;
    public string RecipientSsn { get; set; } = null!;
    public string Role { get; set; } = null!;
    public DateTimeOffset Created { get; set; }
    public bool IsRestricted { get; set; }
}
