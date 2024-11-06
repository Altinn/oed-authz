using oed_authz.Models;

namespace oed_authz.Interfaces;
public interface IOedRoleRepositoryService
{
    public Task<List<RepositoryRoleAssignment>> GetRoleAssignmentsForEstate(string estateSsn, string? filterRecipentSsn = null, string? filterRoleCode = null);
    public Task<List<RepositoryRoleAssignment>> GetRoleAssignmentsForPerson(string recipientSsn, string? filterEstateSsn = null, string? filterRoleCode = null);
    public Task AddRoleAssignment(RepositoryRoleAssignment roleAssignment);
    public Task RemoveRoleAssignment(RepositoryRoleAssignment roleAssignment);
}
