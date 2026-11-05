using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using oed_authz.Infrastructure.Database;
using oed_authz.Models;
using oed_authz.Repositories;
using oed_authz.Services;
using oed_authz.Settings;

namespace oed_authz.IntegrationTests.Services;

public class ProxyManagementServiceAddTests(DatabaseFixture databaseFixture)
    : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private const string HeirSsn = "12345678901";
    private const string RecipientSsnToAdd = "99999999999";

    [Fact]
    public async Task Add_ThrowsUniqueViolationWrappedInDbUpdateException_WhenTheProxyAlreadyExists()
    {
        // Arrange
        var estateSsn = databaseFixture.NextSsn;
        await using (var seedContext = CreateDbContext())
        {
            seedContext.RoleAssignments.Add(new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = HeirSsn,
                RoleCode = Constants.ProbateRoleCode,
            });
            await seedContext.SaveChangesAsync();
        }

        // One context per request, as with a double submit
        await using var firstContext = CreateDbContext();
        await using var secondContext = CreateDbContext();
        await new ProxyManagementService(new RoleAssignmentsRepository(firstContext)).Add(CreateRequest(estateSsn));

        // Act
        var act = () => new ProxyManagementService(new RoleAssignmentsRepository(secondContext)).Add(CreateRequest(estateSsn));

        // Assert
        // AuthorizationController.AddProxy maps exactly this shape to 409 Conflict
        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    private static ProxyManagementRequest CreateRequest(string estateSsn) => new()
    {
        EstateSsn = estateSsn,
        ProxyRoleAssignment = new ProxyRoleAssignment
        {
            HeirSsn = HeirSsn,
            RecipientSsn = RecipientSsnToAdd,
            RoleCode = Constants.IndividualProxyRoleCode,
            Created = DateTimeOffset.UtcNow
        }
    };

    private OedAuthzDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<OedAuthzDbContext>()
            .UseNpgsql(databaseFixture.ConnectionString)
            .Options);

    public async Task InitializeAsync()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
