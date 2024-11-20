namespace oed_authz.Infrastructure.Database;

using Microsoft.EntityFrameworkCore;
using oed_authz.Infrastructure.Database.Model;
using oed_authz.Models;

public class OedAuthzDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<RoleAssignment> RoleAssignments { get; set; }
    //public DbSet<RoleAssignmentLog> RoleassignmentsLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("oedauthz");
        modelBuilder.HasPostgresEnum("oedauthz", "roleassignments_action", new[] { "GRANT", "REVOKE" });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OedAuthzDbContext).Assembly);
    }
}
