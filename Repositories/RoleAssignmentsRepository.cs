using Microsoft.EntityFrameworkCore;
using oed_authz.Infrastructure.Database;
using oed_authz.Interfaces;
using oed_authz.Models;
using System.Text.Json;

namespace oed_authz.Repositories
{
    public class RoleAssignmentsRepository : IRoleAssignmentsRepository
    {
        private readonly OedAuthzDbContext _dbContext;
        private readonly ILogger<RoleAssignmentsRepository> _logger;

        public RoleAssignmentsRepository(OedAuthzDbContext dbContext, ILogger<RoleAssignmentsRepository> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task AddRoleAssignment(RoleAssignment roleAssignment)
        {
            _logger.LogInformation("Granting role: {RoleAssignment}", JsonSerializer.Serialize(roleAssignment));
            await _dbContext.RoleAssignments.AddAsync(roleAssignment);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<List<RoleAssignment>> GetRoleAssignmentsForEstate(string estateSsn, string? filterRecipentSsn = null, string? filterRoleCode = null)
        {
            var query = _dbContext.RoleAssignments
                .Where(ra => ra.EstateSsn == estateSsn);

            if (filterRecipentSsn != null)
            {
                query = query.Where(ra => ra.RecipientSsn == filterRecipentSsn);
            }

            if (filterRoleCode != null)
            {
                query = query.Where(ra => ra.RoleCode == filterRoleCode);
            }

            return await query
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<RoleAssignment>> GetRoleAssignmentsForPerson(string recipientSsn, string? filterEstateSsn = null, string? filterRoleCode = null)
        {
            var query = _dbContext.RoleAssignments
                .Where(ra => ra.RecipientSsn == recipientSsn);

            if (filterEstateSsn != null)
            {
                query = query.Where(ra => ra.EstateSsn == filterEstateSsn);
            }

            if (filterRoleCode != null)
            {
                query = query.Where(ra => ra.RoleCode == filterRoleCode);
            }
            
            return await query
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task RemoveRoleAssignment(RoleAssignment roleAssignment)
        {
            _logger.LogInformation("Revoking role: {RoleAssignment}", JsonSerializer.Serialize(roleAssignment));

            await _dbContext.RoleAssignments
                .Where(ra =>
                    ra.EstateSsn == roleAssignment.EstateSsn
                    && ra.RecipientSsn == roleAssignment.RecipientSsn
                    && ra.RoleCode == roleAssignment.RoleCode
                    && ra.HeirSsn == roleAssignment.HeirSsn)
                .ExecuteDeleteAsync();
        }
    }
}
