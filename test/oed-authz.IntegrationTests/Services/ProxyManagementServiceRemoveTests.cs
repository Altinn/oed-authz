using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using oed_authz.Infrastructure.Database;
using oed_authz.Models;
using oed_authz.Repositories;
using oed_authz.Services;
using oed_authz.Settings;

namespace oed_authz.IntegrationTests.Services;

public class ProxyManagementServiceRemoveTests(DatabaseFixture databaseFixture)
    : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private const string HeirSsn = "12345678901";
    private const string RecipientSsnToRemove = "99999999999";

    [Fact]
    public async Task Remove_ReturnsTrue_WhenTheProxyExisted_AndRemovesIt()
    {
        // Arrange
        var estateSsn = databaseFixture.NextSsn;
        await SeedHeirWithProxy(estateSsn);
        await using var dbContext = CreateDbContext();

        // Act
        var removed = await new ProxyManagementService(new RoleAssignmentsRepository(dbContext)).Remove(CreateRequest(estateSsn));

        // Assert
        removed.Should().BeTrue();
        await using var assertContext = CreateDbContext();
        (await assertContext.RoleAssignments.AnyAsync(ra =>
                ra.EstateSsn == estateSsn && ra.RoleCode == Constants.IndividualProxyRoleCode))
            .Should().BeFalse();
    }

    // A double submit, or a retry after a lost response: only one of the requests removed the proxy, so only
    // that one may report it. AuthorizationController.RemoveProxy answers 404 for the other.
    [Fact]
    public async Task Remove_ReturnsFalse_WhenAnEarlierRequestAlreadyRemovedTheProxy()
    {
        // Arrange
        var estateSsn = databaseFixture.NextSsn;
        await SeedHeirWithProxy(estateSsn);

        // One context per request, as with a double submit
        await using var firstContext = CreateDbContext();
        await using var secondContext = CreateDbContext();
        var first = await new ProxyManagementService(new RoleAssignmentsRepository(firstContext)).Remove(CreateRequest(estateSsn));

        // Act
        var second = await new ProxyManagementService(new RoleAssignmentsRepository(secondContext)).Remove(CreateRequest(estateSsn));

        // Assert
        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    private async Task SeedHeirWithProxy(string estateSsn)
    {
        await using var seedContext = CreateDbContext();
        seedContext.RoleAssignments.AddRange(
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = HeirSsn,
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                HeirSsn = HeirSsn,
                RecipientSsn = RecipientSsnToRemove,
                RoleCode = Constants.IndividualProxyRoleCode,
                Created = DateTimeOffset.UtcNow
            });
        await seedContext.SaveChangesAsync();
    }

    private static ProxyManagementRequest CreateRequest(string estateSsn) => new()
    {
        EstateSsn = estateSsn,
        ProxyRoleAssignment = new ProxyRoleAssignment
        {
            HeirSsn = HeirSsn,
            RecipientSsn = RecipientSsnToRemove,
            RoleCode = Constants.IndividualProxyRoleCode
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
