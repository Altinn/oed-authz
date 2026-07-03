using Microsoft.EntityFrameworkCore;
using oed_authz.Infrastructure.Database;
using oed_authz.Interfaces;
using oed_authz.Models;

namespace oed_authz.Repositories;

public class RoleAssignmentsRepository : IRoleAssignmentsRepository
{
    private readonly OedAuthzDbContext _dbContext;

    public RoleAssignmentsRepository(OedAuthzDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddRoleAssignment(RoleAssignment roleAssignment)
    {
        await _dbContext.RoleAssignments.AddAsync(roleAssignment);
        await _dbContext.SaveChangesAsync();
    }

    public Task<List<RoleAssignment>> GetRoleAssignmentsForEstate(string estateSsn)
    {
        return _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<RoleAssignment>> GetRoleAssignmentsForPerson(string estateSsn, string recipientSsn)
    {
        if (string.IsNullOrWhiteSpace(recipientSsn))
            return Task.FromResult(new List<RoleAssignment>());

        return _dbContext.RoleAssignments
            .Where(ra =>
                ra.EstateSsn == estateSsn
                && ra.RecipientSsn == recipientSsn)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<RoleAssignment>> GetAllRoleAssignmentsForPerson(string ssn)
    {
        if (string.IsNullOrWhiteSpace(ssn))
            return Task.FromResult(new List<RoleAssignment>());

        return _dbContext.RoleAssignments
            .Where(ra => ra.HeirSsn == ssn || ra.RecipientSsn == ssn)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task RemoveRoleAssignment(RoleAssignment roleAssignment)
    {
        return _dbContext.RoleAssignments
            .Where(ra =>
                ra.EstateSsn == roleAssignment.EstateSsn
                && ra.RecipientSsn == roleAssignment.RecipientSsn
                && ra.RoleCode == roleAssignment.RoleCode
                && ra.HeirSsn == roleAssignment.HeirSsn)
            .ExecuteDeleteAsync();
    }
}
