namespace oed_authz.Models.Dto;

public class PipResponseDto
{
    public List<PipRoleAssignmentDto> RoleAssignments { get; set; } = new();
}
