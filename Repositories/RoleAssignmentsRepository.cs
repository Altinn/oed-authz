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

        public RoleAssignmentsRepository(
            OedAuthzDbContext dbContext, 
            ILogger<RoleAssignmentsRepository> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task AddRoleAssignment(RoleAssignment roleAssignment)
        {
            _logger.LogInformation("Granting role: {RoleAssignment}", 
                JsonSerializer.Serialize(roleAssignment));

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
            return _dbContext.RoleAssignments
                .Where(ra =>                     
                    ra.EstateSsn == estateSsn
                    && ra.RecipientSsn == recipientSsn)
                .AsNoTracking()
                .ToListAsync();
        }

        public Task RemoveRoleAssignment(RoleAssignment roleAssignment)
        {
            _logger.LogInformation("Revoking role: {RoleAssignment}",
                JsonSerializer.Serialize(roleAssignment));

            return _dbContext.RoleAssignments
                .Where(ra =>
                    ra.EstateSsn == roleAssignment.EstateSsn
                    && ra.RecipientSsn == roleAssignment.RecipientSsn
                    && ra.RoleCode == roleAssignment.RoleCode
                    && ra.HeirSsn == roleAssignment.HeirSsn)
                .ExecuteDeleteAsync();
        }
    }
}
