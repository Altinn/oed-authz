using oed_authz.Models;

namespace oed_authz.Interfaces;

public interface IRoleAssignmentsRepository
{
    public Task<List<RoleAssignment>> GetRoleAssignmentsForEstate(string estateSsn);
    public Task<List<RoleAssignment>> GetRoleAssignmentsForPerson(string estateSsn, string recipientSsn);
    public Task AddRoleAssignment(RoleAssignment roleAssignment);
    public Task RemoveRoleAssignment(RoleAssignment roleAssignment);
}