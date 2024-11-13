using oed_authz.Models;

namespace oed_authz.Interfaces;

public interface IRoleAssignmentsRepository
{
    public Task<List<RoleAssignment>> GetRoleAssignmentsForEstate(string estateSsn, string? filterRecipentSsn = null, string? filterRoleCode = null);
    public Task<List<RoleAssignment>> GetRoleAssignmentsForPerson(string recipientSsn, string? filterEstateSsn = null, string? filterRoleCode = null);
    public Task AddRoleAssignment(RoleAssignment roleAssignment);
    public Task RemoveRoleAssignment(RoleAssignment roleAssignment);
}