using System.Text.Json;
using FakeItEasy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using oed_authz.Infrastructure.Database;
using oed_authz.Models;
using oed_authz.Models.Dto;
using oed_authz.Repositories;
using oed_authz.Services;
using oed_authz.Settings;

namespace oed_authz.IntegrationTests.Services;

public class EventHandlerServiceTests : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly OedAuthzDbContext _dbContext;
    private readonly AltinnEventHandlerService _sut;

    public EventHandlerServiceTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
        _dbContext = new OedAuthzDbContext(
            new DbContextOptionsBuilder<OedAuthzDbContext>()
                .UseNpgsql(databaseFixture.ConnectionString)
                .Options);

        var roleAssignmentsRepository = new RoleAssignmentsRepository(
            _dbContext,
            A.Fake<ILogger<RoleAssignmentsRepository>>());

        var proxyManagementService =
            new ProxyManagementService(roleAssignmentsRepository, A.Fake<ILogger<ProxyManagementService>>());

        _sut = new AltinnEventHandlerService(
            roleAssignmentsRepository,
            proxyManagementService,
            A.Fake<ILogger<AltinnEventHandlerService>>());
    }

    [Fact]
    public async Task HandleEvent_ShouldCreateRolesAccordingToCloudEvent_WhenEstateIsCreated()
    {
     
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;

        var eventRoleAssignments = new EventRoleAssignmentDataDto
        {
            DaCaseId = Guid.NewGuid().ToString(),
            CaseStatus = CaseStatus.MOTTATT,
            HeirRoles =
            [
                new EventRoleAssignmentDto
                {
                    Nin = "99999999991",
                    Role = Constants.FormuesfullmaktRoleCode
                },
                new EventRoleAssignmentDto
                {
                    Nin = "99999999992",
                    Role = Constants.FormuesfullmaktRoleCode
                },
            ]
        };

        var cloudEvent = new CloudEvent
        {
            Type = Events.Oed.CaseStatusUpdateValidated,
            Subject = $"person/{estateSsn}",
            Data = JsonSerializer.Serialize(eventRoleAssignments)
        };

        // Act
        await _sut.HandleEvent(cloudEvent);

        // Assert
        var roleAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        roleAssignments.Should().HaveCount(2);
        roleAssignments.Should().ContainSingle(ra => 
            ra.RecipientSsn == "99999999991" && 
            ra.RoleCode == Constants.FormuesfullmaktRoleCode);
        roleAssignments.Should().ContainSingle(ra =>
            ra.RecipientSsn == "99999999992" &&
            ra.RoleCode == Constants.FormuesfullmaktRoleCode);
    }

    [Fact]
    public async Task HandleEvent_ShouldRemoveAllRoles_WhenEstateIsFeilfort()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;

        _dbContext.RoleAssignments.AddRange(
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "99999999991",
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "99999999992",
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "99999999993",
                RoleCode = Constants.FormuesfullmaktRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "99999999999",
                HeirSsn = "99999999991",
                RoleCode = Constants.IndividualProxyRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = "99999999999",
                RoleCode = Constants.CollectiveProxyRoleCode,
            });

        await _dbContext.SaveChangesAsync();


        var eventRoleAssignments = new EventRoleAssignmentDataDto
        {
            DaCaseId = Guid.NewGuid().ToString(),
            CaseStatus = CaseStatus.FEILFORT,
        };

        var cloudEvent = new CloudEvent
        {
            Type = Events.Oed.CaseStatusUpdateValidated,
            Subject = $"person/{estateSsn}",
            Data = JsonSerializer.Serialize(eventRoleAssignments)
        };

        // Act
        await _sut.HandleEvent(cloudEvent);

        // Assert
        var roleAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        roleAssignments.Should().HaveCount(0);
    }
    
    public Task InitializeAsync() => _dbContext.Database.MigrateAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}