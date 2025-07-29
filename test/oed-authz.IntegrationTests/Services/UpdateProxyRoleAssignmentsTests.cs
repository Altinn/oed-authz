using FakeItEasy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using oed_authz.Infrastructure.Database;
using oed_authz.Models;
using oed_authz.Repositories;
using oed_authz.Services;
using oed_authz.Settings;

namespace oed_authz.IntegrationTests.Services;


public class UpdateProxyRoleAssignmentsTests : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly OedAuthzDbContext _dbContext;
    private readonly ProxyManagementService _sut;

    public UpdateProxyRoleAssignmentsTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
        _dbContext = new OedAuthzDbContext(
            new DbContextOptionsBuilder<OedAuthzDbContext>()
                .UseNpgsql(databaseFixture.ConnectionString)
                .Options);
        
        var roleAssignmentsRepository = new RoleAssignmentsRepository(
            _dbContext,
            A.Fake<ILogger<RoleAssignmentsRepository>>());

        _sut = new ProxyManagementService(roleAssignmentsRepository, A.Fake<ILogger<ProxyManagementService>>());
    }

    [Fact]
    public async Task UpdateProxyRoleAssigments_ShouldRemoveProxyRole_WhenHeirDontHaveProbateRole()
    {
        // Arrange
        var estateSsn =  _databaseFixture.NextSsn;

        var prevAssignments = new []
        {
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678901",
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678902",
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678903",
                RoleCode = Constants.FormuesfullmaktRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "99999999999",
                HeirSsn = "12345678903",                            // Heir without the ProbateRoleCode
                RoleCode = Constants.IndividualProxyRoleCode,
            }
        };

        _dbContext.RoleAssignments.AddRange(prevAssignments);
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.UpdateProxyRoleAssigments(estateSsn);

        // Assert
        var updatedAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        updatedAssignments.Should().HaveCount(3);
        updatedAssignments.Should().NotContain(ra => ra.RoleCode == Constants.IndividualProxyRoleCode);
        updatedAssignments.Should().NotContain(ra => ra.RoleCode == Constants.CollectiveProxyRoleCode);
    }

    [Fact]
    public async Task UpdateProxyRoleAssigments_ShouldRemoveProxyRole_WhenHeirIsRemoved()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;

        var prevAssignments = new[]
        {
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678901",
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678902",
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "99999999999",
                HeirSsn = "12345678903",                            // Heir that has been removed
                RoleCode = Constants.IndividualProxyRoleCode,
            }
        };

        _dbContext.RoleAssignments.AddRange(prevAssignments);
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.UpdateProxyRoleAssigments(estateSsn);

        // Assert
        var updatedAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        updatedAssignments.Should().HaveCount(2);
        updatedAssignments.Should().NotContain(ra => ra.RoleCode == Constants.IndividualProxyRoleCode);
        updatedAssignments.Should().NotContain(ra => ra.RoleCode == Constants.CollectiveProxyRoleCode);
    }

    [Fact]
    public async Task UpdateProxyRoleAssigments_ShouldAddCollectiveProxyRole_WhenIndividualProxyRoleIsGivenByTheOnlyOtherHeir()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;

        var prevAssignments = new[]
        {
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678901",
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678902",
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678901",
                HeirSsn = "12345678902",                            
                RoleCode = Constants.IndividualProxyRoleCode,
            }
        };

        _dbContext.RoleAssignments.AddRange(prevAssignments);
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.UpdateProxyRoleAssigments(estateSsn);

        // Assert
        var updatedAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        updatedAssignments.Should().HaveCount(4);
        updatedAssignments.Should().Contain(ra => 
            ra.RoleCode == Constants.CollectiveProxyRoleCode && 
            ra.RecipientSsn == "12345678901");
    }

    [Fact]
    public async Task UpdateProxyRoleAssigments_ShouldRemoveCollectiveAndIndividualProxyRole_WhenHeirDontHaveProbateRole()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;

        var prevAssignments = new[]
        {
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678901",
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678902",
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678901",                       
                HeirSsn = "12345678903",                            // Removed heir
                RoleCode = Constants.IndividualProxyRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678901",
                RoleCode = Constants.CollectiveProxyRoleCode,
            }
        };

        _dbContext.RoleAssignments.AddRange(prevAssignments);
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.UpdateProxyRoleAssigments(estateSsn);

        // Assert
        var updatedAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        updatedAssignments.Should().HaveCount(2);
        updatedAssignments.Should().NotContain(ra => ra.RoleCode == Constants.IndividualProxyRoleCode);
        updatedAssignments.Should().NotContain(ra => ra.RoleCode == Constants.CollectiveProxyRoleCode);
        updatedAssignments.Should().ContainSingle(ra => ra.RecipientSsn == "12345678901");
    }

    [Fact]
    public async Task UpdateProxyRoleAssigments_ShouldRemoveCollectiveAndIndividualProxyRole_WhenHeirIsRemoved()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;

        var prevAssignments = new[]
        {
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678901",
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678902",
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678903",
                RoleCode = Constants.FormuesfullmaktRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678901",
                HeirSsn = "12345678903",                            // Heir without probate role
                RoleCode = Constants.IndividualProxyRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "12345678901",
                RoleCode = Constants.CollectiveProxyRoleCode,
            }
        };

        _dbContext.RoleAssignments.AddRange(prevAssignments);
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.UpdateProxyRoleAssigments(estateSsn);

        // Assert
        var updatedAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        updatedAssignments.Should().HaveCount(3);
        updatedAssignments.Should().NotContain(ra => ra.RoleCode == Constants.IndividualProxyRoleCode);
        updatedAssignments.Should().NotContain(ra => ra.RoleCode == Constants.CollectiveProxyRoleCode);
        updatedAssignments.Should().ContainSingle(ra => ra.RecipientSsn == "12345678901");
    }

    public Task InitializeAsync() => _dbContext.Database.MigrateAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}