using Altinn.Dd.InternalEvents;
using Altinn.Dd.InternalEvents.Estate;
using FakeItEasy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using oed_authz.Infrastructure.Database;
using oed_authz.Infrastructure.Database.Model;
using oed_authz.Models;
using oed_authz.Repositories;
using oed_authz.Services;
using oed_authz.Settings;
using System.Text.Json;

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

        var eventCursorRepo = new EventCursorRepository(_dbContext);

        var roleAssignmentsRepository = new RoleAssignmentsRepository(_dbContext);

        var proxyManagementService =
            new ProxyManagementService(roleAssignmentsRepository);

        _sut = new AltinnEventHandlerService(
            _dbContext,
            eventCursorRepo,
            roleAssignmentsRepository,
            proxyManagementService,
            A.Fake<ILogger<AltinnEventHandlerService>>());
    }

    [Fact]
    public async Task HandleEvent_ShouldCreateRolesAccordingToCloudEvent_WhenEstateIsCreated()
    {

        // Arrange
        var estateSsn = _databaseFixture.NextSsn;

        var eventRoleAssignments = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = Guid.NewGuid().ToString(),
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = [
                new PersonHeirRole
                {
                    Nin = "99999999991",
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn,
                },
                new PersonHeirRole
                {
                    Nin = "99999999992",
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn,
                }
            ]
        };

        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.Now,
            Type = EventType.CaseStatusUpdateValidated,
            //Subject = $"person/{estateSsn}",
            Subject = estateSsn,
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


        var eventRoleAssignments = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = Guid.NewGuid().ToString(),
            CaseStatus = CaseStatus.Feilfort,
            HeirRolesV2 = []
        };

        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.Now,
            Type = EventType.CaseStatusUpdateValidated,
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

    [Fact]
    public async Task HandleEvent_WithNoExistingEventCursor_ShouldInsertEventCursor_OnSuccess()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;
        var daCaseId = Guid.NewGuid().ToString();
        var timestamp = new DateTimeOffset(2025, 8, 1, 18, 0, 0, TimeSpan.Zero);

        var roleAssignments = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = daCaseId,
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = [
                new PersonHeirRole
                {
                    Nin = "99999999991",
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn,
                }
            ]
        };

        var cloudEvent = new CloudEvent
        {
            Time = timestamp,
            Type = EventType.CaseStatusUpdateValidated,
            Subject = $"person/{estateSsn}",
            Data = JsonSerializer.Serialize(roleAssignments),
        };

        // Act
        await _sut.HandleEvent(cloudEvent);

        // Assert
        var cursor = _dbContext.Set<EventCursor>()
            .Single(c => c.EstateSsn == estateSsn);

        cursor.Should().NotBeNull();
        cursor.LastTimestampProcessed.Should().Be(timestamp);

        var arrangedRoleAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        arrangedRoleAssignments.Should().HaveCount(1);
        arrangedRoleAssignments.Should().OnlyContain(ra => ra.RoleCode == Constants.FormuesfullmaktRoleCode);
    }

    [Fact]
    public async Task HandleEvent_WithExisitingEventCursor_ShouldUpdateEventCursor_OnSuccess()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;
        var daCaseId = Guid.NewGuid().ToString();
        var timestamp = new DateTimeOffset(2025, 8, 1, 18, 0, 0, TimeSpan.Zero);

        var arrangeRoleAssignments = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = daCaseId,
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = []
        };

        var arrangeCloudEvent = new CloudEvent
        {
            Time = timestamp.Subtract(TimeSpan.FromSeconds(1)),
            Type = EventType.CaseStatusUpdateValidated,
            Subject = $"person/{estateSsn}",
            Data = JsonSerializer.Serialize(arrangeRoleAssignments),
        };

        // First call to create cursor
        await _sut.HandleEvent(arrangeCloudEvent);

        // Verify preconditions
        var arrangeCursor = _dbContext.Set<EventCursor>().Single(c => c.EstateSsn == estateSsn);
        arrangeCursor.LastTimestampProcessed.Should().Be(timestamp.Subtract(TimeSpan.FromSeconds(1)));

        // Act
        var roleAssignments = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = daCaseId,
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = [
                new PersonHeirRole
                {
                    Nin = "99999999991",
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn,
                }
            ]
        };

        var cloudEvent = new CloudEvent
        {
            Time = timestamp,
            Type = EventType.CaseStatusUpdateValidated,
            Subject = $"person/{estateSsn}",
            Data = JsonSerializer.Serialize(roleAssignments),
        };

        await _sut.HandleEvent(cloudEvent);

        // Assert
        var cursor = _dbContext.Set<EventCursor>().Single(c => c.EstateSsn == estateSsn);
        cursor.LastTimestampProcessed.Should().Be(timestamp);

        var resultRoleAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        resultRoleAssignments.Should().HaveCount(1);
        resultRoleAssignments.Should().OnlyContain(ra => ra.RoleCode == Constants.FormuesfullmaktRoleCode);
    }

    [Fact]
    public async Task HandleEvent_WithExisitingEventCursor_ShouldNotUpdateEventCursor_OnFail()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;
        var daCaseId = Guid.NewGuid().ToString();
        var timestamp = new DateTimeOffset(2025, 8, 1, 18, 0, 0, TimeSpan.Zero);

        var arrangeRoleAssignments = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = daCaseId,
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = []
        };

        var arrangeCloudEvent = new CloudEvent
        {
            Time = timestamp.Subtract(TimeSpan.FromSeconds(1)),
            Type = EventType.CaseStatusUpdateValidated,
            Subject = $"person/{estateSsn}",
            Data = JsonSerializer.Serialize(arrangeRoleAssignments),
        };

        // First call to create cursor
        await _sut.HandleEvent(arrangeCloudEvent);

        // Verify preconditions
        var arrangeCursor = _dbContext.Set<EventCursor>().Single(c => c.EstateSsn == estateSsn);
        arrangeCursor.LastTimestampProcessed.Should().Be(timestamp.Subtract(TimeSpan.FromSeconds(1)));

        // Act
        var roleAssignments = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = daCaseId,
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = [
                new PersonHeirRole
                {
                    Nin = "", // Should throws argument exception
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn,
                }
            ]
        };

        var cloudEvent = new CloudEvent
        {
            Time = timestamp,
            Type = EventType.CaseStatusUpdateValidated,
            Subject = $"person/{estateSsn}",
            Data = JsonSerializer.Serialize(roleAssignments),
        };

        var act = async () => await _sut.HandleEvent(cloudEvent);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        var cursor = _dbContext.Set<EventCursor>().Single(c => c.EstateSsn == estateSsn);
        cursor.LastTimestampProcessed.Should().Be(arrangeCursor.LastTimestampProcessed);

        var resultRoleAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        resultRoleAssignments.Should().HaveCount(0);
    }

    [Fact]
    public async Task HandleEvent_WithNoExistingEventCursor_ShouldNotInsertEventCursor_OnFail()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;
        var daCaseId = Guid.NewGuid().ToString();
        var timestamp = new DateTimeOffset(2025, 8, 1, 18, 0, 0, TimeSpan.Zero);

        var roleAssignments = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = daCaseId,
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = [
                new PersonHeirRole
                {
                    Nin = "", // Should throws argument exception
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn,
                }
            ]
        };

        var cloudEvent = new CloudEvent
        {
            Time = timestamp,
            Type = EventType.CaseStatusUpdateValidated,
            Subject = $"person/{estateSsn}",
            Data = JsonSerializer.Serialize(roleAssignments),
        };

        // Act
        var act = async () => await _sut.HandleEvent(cloudEvent);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        var cursor = _dbContext.Set<EventCursor>()
            .SingleOrDefault(c => c.EstateSsn == estateSsn);

        cursor.Should().BeNull();

        var arrangedRoleAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        arrangedRoleAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleEvent_OutOfOrderEvents_ShouldBeDiscarded()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;
        var daCaseId = Guid.NewGuid().ToString();

        var latestRoleAssignments = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = daCaseId,
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = [
                new PersonHeirRole
                {
                    Nin = "99999999991",
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn,
                }
            ]
        };

        var latestEvent = new CloudEvent
        {
            Time = new DateTimeOffset(2025, 8, 1, 18, 0, 0, TimeSpan.Zero), // 2025-08-01T18:00:00+00
            Type = EventType.CaseStatusUpdateValidated,
            Subject = $"person/{estateSsn}",
            Data = JsonSerializer.Serialize(latestRoleAssignments),
        };

        await _sut.HandleEvent(latestEvent);

        // Verifying the preconditions
        var arrangedRoleAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        arrangedRoleAssignments.Should().HaveCount(1);
        arrangedRoleAssignments.Should().OnlyContain(ra => ra.RoleCode == Constants.FormuesfullmaktRoleCode);

        // Act
        var outOfOrderRoleAssignments = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = daCaseId,
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = []
        };

        var outOfOrderEvent = new CloudEvent
        {
            Time = new DateTimeOffset(2025, 8, 1, 17, 0, 0, TimeSpan.Zero), // 2025-08-01T17:00:00+00
            Type = EventType.CaseStatusUpdateValidated,
            Subject = $"person/{estateSsn}",
            Data = JsonSerializer.Serialize(outOfOrderRoleAssignments),
        };

        await _sut.HandleEvent(outOfOrderEvent);

        // Assert - The arranged roles should not have been changed => the out of order event withhout heirs have been discarded and not processed.
        var resultRoleAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        resultRoleAssignments.Should().HaveCount(1);
        resultRoleAssignments.Should().OnlyContain(rra =>
            rra.RoleCode == Constants.FormuesfullmaktRoleCode && // No changes in role codes
            arrangedRoleAssignments.Any(ara =>
                ara.Id == rra.Id)); // All the same ids as before. No delete with new inserts.
    }

    [Fact]
    public async Task HandleEvent_EventsWithExactlySameTimestamp_LastEventShouldBeDiscarded()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;
        var daCaseId = Guid.NewGuid().ToString();
        var timestamp = new DateTimeOffset(2025, 8, 1, 18, 0, 0, TimeSpan.Zero);

        var roleAssignments = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = daCaseId,
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = [
                new PersonHeirRole
                {
                    Nin = "99999999991",
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn,
                }
            ]
        };

        var cloudEvent = new CloudEvent
        {
            Time = timestamp,
            Type = EventType.CaseStatusUpdateValidated,
            Subject = $"person/{estateSsn}",
            Data = JsonSerializer.Serialize(roleAssignments),
        };

        await _sut.HandleEvent(cloudEvent);

        // Verifying the preconditions
        var arrangedRoleAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        arrangedRoleAssignments.Should().HaveCount(1);
        arrangedRoleAssignments.Should().OnlyContain(ra => ra.RoleCode == Constants.FormuesfullmaktRoleCode);

        // Act
        var anotherRoleAssignments = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = daCaseId,
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = []
        };

        var anotherEvent = new CloudEvent
        {
            Time = timestamp, // Same timestamp as the previus event
            Type = EventType.CaseStatusUpdateValidated,
            Subject = $"person/{estateSsn}",
            Data = JsonSerializer.Serialize(anotherRoleAssignments),
        };

        await _sut.HandleEvent(anotherEvent);

        // Assert - The arranged roles should not have been changed => the out of order event withhout heirs have been discarded and not processed.
        var resultRoleAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        resultRoleAssignments.Should().HaveCount(1);
        resultRoleAssignments.Should().OnlyContain(rra =>
            rra.RoleCode == Constants.FormuesfullmaktRoleCode && // No changes in role codes
            arrangedRoleAssignments.Any(ara =>
                ara.Id == rra.Id)); // All the same ids as before. No delete with new inserts.
    }

    [Fact]
    public async Task HandleEvent_V2EventWithAllPartTypes_ShouldOnlyAssignRolesToPersonsWithSkiftefullmakt()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;

        var eventRoleAssignments = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = Guid.NewGuid().ToString(),
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = [
                new PersonHeirRole
                {
                    Nin = "99999999991",
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn
                },
                new PersonHeirRole
                {
                    Nin = "99999999992",
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn
                },
                new PersonHeirRole
                {
                    Nin = "99999999993",
                    Role = Constants.NoRoleCode,    // Person without formuesfullmakt => role = null
                    Relation = HeirRoleRelation.Barn
                },
                new PappPersonHeirRole
                {
                    Name = new PersonName
                    {
                        FirstName = "Papp",
                        MiddleName = ["Pappson"],
                        LastName = "Person"
                    },
                    DateOfBirth = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    Relation = HeirRoleRelation.Barn,
                },
                new OrganizationHeirRole
                {
                    OrgNo = "123456789",
                    Relation = HeirRoleRelation.TestamentarvingBegrenset,
                },
                new PappOrganizationHeirRole
                {
                    Name = "PappOrg",
                    Relation = HeirRoleRelation.TestamentarvingBegrenset,
                }
            ]
        };

        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.Now,
            Type = EventType.CaseStatusUpdateValidated,
            //Subject = $"person/{estateSsn}",
            Subject = estateSsn,
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
    public async Task HandleEvent_V2EventWithPersonParts_ShouldAssignRolesToPersonsWithSkiftefullmaktOrProbateRoleCode()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;

        var eventRoleAssignments = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = Guid.NewGuid().ToString(),
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = [
                new PersonHeirRole
                {
                    Nin = "99999999991",
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn
                },
                new PersonHeirRole
                {
                    Nin = "99999999992",
                    Role = Constants.ProbateRoleCode,
                    Relation = HeirRoleRelation.Barn
                },
                new PersonHeirRole
                {
                    Nin = "99999999993",
                    Role = Constants.NoRoleCode,    // Person without formuesfullmakt => role = null
                    Relation = HeirRoleRelation.Barn
                }
            ]
        };

        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.Now,
            Type = EventType.CaseStatusUpdateValidated,
            //Subject = $"person/{estateSsn}",
            Subject = estateSsn,
            Data = JsonSerializer.Serialize(eventRoleAssignments)
        };

        // Act
        await _sut.HandleEvent(cloudEvent);

        // Assert
        var roleAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        roleAssignments.Should().HaveCount(3);
        roleAssignments.Should().ContainSingle(ra =>
            ra.RecipientSsn == "99999999991" &&
            ra.RoleCode == Constants.FormuesfullmaktRoleCode);
        roleAssignments.Should().ContainSingle(ra =>
            ra.RecipientSsn == "99999999992" &&
            ra.RoleCode == Constants.ProbateRoleCode);
        roleAssignments.Should().ContainSingle(ra =>
            ra.RecipientSsn == "99999999992" &&
            ra.RoleCode == Constants.CollectiveProxyRoleCode);
        roleAssignments.Should().NotContain(ra =>
            ra.RecipientSsn == "99999999993");
    }

    [Fact]
    public async Task HandleEvent_CaseStatusManuallySynced_ShouldCreateRolesAccordingToEvent()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;

        var eventRoleAssignments = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = Guid.NewGuid().ToString(),
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = [
                new PersonHeirRole
                {
                    Nin = "99999999991",
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn,
                },
                new PersonHeirRole
                {
                    Nin = "99999999992",
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn,
                }
            ]
        };

        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.Now,
            Type = EventType.CaseStatusManuallySynced,
            Subject = estateSsn,
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
    public async Task HandleEvent_CaseStatusManuallySynced_HasIndependentEventCursorFromCaseStatusUpdateValidated()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;
        var laterTimestamp = new DateTimeOffset(2025, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var earlierTimestamp = laterTimestamp.Subtract(TimeSpan.FromHours(1));

        var eventData = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = Guid.NewGuid().ToString(),
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = [
                new PersonHeirRole
                {
                    Nin = "99999999991",
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn,
                }
            ]
        };

        // Send a CaseStatusUpdateValidated event at the later timestamp
        var validatedEvent = new CloudEvent
        {
            Time = laterTimestamp,
            Type = EventType.CaseStatusUpdateValidated,
            Subject = estateSsn,
            Data = JsonSerializer.Serialize(eventData)
        };

        await _sut.HandleEvent(validatedEvent);

        // Act: send a CaseStatusManuallySynced event at an earlier timestamp for the same estate.
        // This would be discarded if it shared the CaseStatusUpdateValidated cursor, but it has its own.
        var manuallySyncedEventData = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = Guid.NewGuid().ToString(),
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = [
                new PersonHeirRole
                {
                    Nin = "99999999992",
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn,
                }
            ]
        };

        var manuallySyncedEvent = new CloudEvent
        {
            Time = earlierTimestamp,
            Type = EventType.CaseStatusManuallySynced,
            Subject = estateSsn,
            Data = JsonSerializer.Serialize(manuallySyncedEventData)
        };

        await _sut.HandleEvent(manuallySyncedEvent);

        // Assert: both events were processed — two separate cursors exist for this estate
        var cursors = _dbContext.Set<EventCursor>()
            .Where(c => c.EstateSsn == estateSsn)
            .ToList();

        cursors.Should().HaveCount(2);
        cursors.Should().ContainSingle(c =>
            c.EventType == EventType.CaseStatusUpdateValidated &&
            c.LastTimestampProcessed == laterTimestamp);
        cursors.Should().ContainSingle(c =>
            c.EventType == EventType.CaseStatusManuallySynced &&
            c.LastTimestampProcessed == earlierTimestamp);

        // The CaseStatusManuallySynced event was processed (not discarded), which means it
        // reconciled roles based on its own data and replaced the heir from the earlier event.
        // If the cursors were shared, the manually-synced event would have been discarded
        // (its timestamp is earlier), leaving 99999999991 as the only heir.
        var roleAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        roleAssignments.Should().HaveCount(1);
        roleAssignments.Should().ContainSingle(ra => ra.RecipientSsn == "99999999992");
    }

    [Fact]
    public async Task HandleEvent_CaseStatusManuallySynced_OutOfOrderEvents_ShouldBeDiscarded()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;
        var daCaseId = Guid.NewGuid().ToString();

        var firstEventData = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = daCaseId,
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = [
                new PersonHeirRole
                {
                    Nin = "99999999991",
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn,
                }
            ]
        };

        var firstEvent = new CloudEvent
        {
            Time = new DateTimeOffset(2025, 9, 1, 18, 0, 0, TimeSpan.Zero),
            Type = EventType.CaseStatusManuallySynced,
            Subject = estateSsn,
            Data = JsonSerializer.Serialize(firstEventData),
        };

        await _sut.HandleEvent(firstEvent);

        // Verify preconditions
        var arrangedRoleAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        arrangedRoleAssignments.Should().HaveCount(1);

        // Act: send an older CaseStatusManuallySynced event — should be discarded
        var outOfOrderEventData = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = daCaseId,
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = []
        };

        var outOfOrderEvent = new CloudEvent
        {
            Time = new DateTimeOffset(2025, 9, 1, 17, 0, 0, TimeSpan.Zero),
            Type = EventType.CaseStatusManuallySynced,
            Subject = estateSsn,
            Data = JsonSerializer.Serialize(outOfOrderEventData),
        };

        await _sut.HandleEvent(outOfOrderEvent);

        // Assert: roles unchanged — out-of-order event was discarded
        var resultRoleAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        resultRoleAssignments.Should().HaveCount(1);
        resultRoleAssignments.Should().OnlyContain(rra =>
            rra.RoleCode == Constants.FormuesfullmaktRoleCode &&
            arrangedRoleAssignments.Any(ara => ara.Id == rra.Id));
    }

    [Fact]
    public async Task HandleEvent_FregProtectedAddressUpdate_ShouldWipeEveryEstateThePersonIsPartOf_AndLeaveOthersUntouched()
    {
        // Arrange
        var protectedPersonSsn = _databaseFixture.NextSsn;
        var otherHeirSsn = _databaseFixture.NextSsn;
        var estateA = _databaseFixture.NextSsn;
        var estateB = _databaseFixture.NextSsn;
        var unaffectedEstate = _databaseFixture.NextSsn;

        _dbContext.RoleAssignments.AddRange(
            // Estate A: protected person is a recipient, alongside another heir
            new RoleAssignment
            {
                EstateSsn = estateA,
                RecipientSsn = protectedPersonSsn,
                RoleCode = Constants.FormuesfullmaktRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateA,
                RecipientSsn = otherHeirSsn,
                RoleCode = Constants.FormuesfullmaktRoleCode,
            },
            // Estate B: protected person appears only as the HeirSsn on a proxy assignment
            new RoleAssignment
            {
                EstateSsn = estateB,
                RecipientSsn = otherHeirSsn,
                HeirSsn = protectedPersonSsn,
                RoleCode = Constants.IndividualProxyRoleCode,
            },
            // Unaffected estate: protected person not involved at all
            new RoleAssignment
            {
                EstateSsn = unaffectedEstate,
                RecipientSsn = otherHeirSsn,
                RoleCode = Constants.FormuesfullmaktRoleCode,
            });

        await _dbContext.SaveChangesAsync();

        // Freg events arrive on the wire as { "nin": "..." }
        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.UtcNow,
            Type = EventType.FregProtectedAddressUpdate,
            Subject = protectedPersonSsn,
            Data = JsonSerializer.Serialize(new { nin = protectedPersonSsn })
        };

        // Act
        await _sut.HandleEvent(cloudEvent);

        // Assert - estates A and B are wiped entirely (every heir, not just the protected person)
        _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateA)
            .ToList()
            .Should().BeEmpty();

        _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateB)
            .ToList()
            .Should().BeEmpty();

        // The unrelated estate is left untouched
        _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == unaffectedEstate)
            .ToList()
            .Should().ContainSingle(ra => ra.RecipientSsn == otherHeirSsn);
    }

    [Fact]
    public async Task HandleEvent_FregProtectedAddressUpdate_ShouldWipeEntireEstate_IncludingAutoManagedCollectiveProxy_CreatedByEstateEvent()
    {
        // Arrange - build the estate's role assignments through the real estate-event path,
        // so the data shape (court roles + the auto-managed collective proxy) matches production.
        var estateSsn = _databaseFixture.NextSsn;
        var protectedHeirSsn = _databaseFixture.NextSsn; // probate role -> auto-assigned collective proxy
        var otherHeirSsn = _databaseFixture.NextSsn;      // formuesfullmakt role

        var estateEventData = new EstateCaseUpdatedEvent
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = Guid.NewGuid().ToString(),
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = [
                new PersonHeirRole
                {
                    Nin = protectedHeirSsn,
                    Role = Constants.ProbateRoleCode,
                    Relation = HeirRoleRelation.Barn,
                },
                new PersonHeirRole
                {
                    Nin = otherHeirSsn,
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn,
                }
            ]
        };

        var estateEvent = new CloudEvent
        {
            Time = DateTimeOffset.UtcNow,
            Type = EventType.CaseStatusUpdateValidated,
            Subject = estateSsn,
            Data = JsonSerializer.Serialize(estateEventData)
        };

        await _sut.HandleEvent(estateEvent);

        // Precondition: the estate event produced both court roles plus an auto-managed collective proxy
        var seededAssignments = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        seededAssignments.Should().Contain(ra =>
            ra.RecipientSsn == protectedHeirSsn && ra.RoleCode == Constants.ProbateRoleCode);
        seededAssignments.Should().Contain(ra =>
            ra.RecipientSsn == otherHeirSsn && ra.RoleCode == Constants.FormuesfullmaktRoleCode);
        seededAssignments.Should().Contain(ra =>
            ra.RoleCode == Constants.CollectiveProxyRoleCode);

        // Act: protected-address update for the probate heir
        var fregEvent = new CloudEvent
        {
            Time = DateTimeOffset.UtcNow,
            Type = EventType.FregProtectedAddressUpdate,
            Subject = protectedHeirSsn,
            Data = JsonSerializer.Serialize(new { nin = protectedHeirSsn })
        };

        await _sut.HandleEvent(fregEvent);

        // Assert: the entire estate is wiped - court roles, the other heir, and the
        // auto-managed collective proxy all gone.
        _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task HandleEvent_EstateEventAfterFregWipe_ShouldReGrantCourtRoles_DocumentingThatWipeIsNotDurable()
    {
        // This test documents (it does NOT endorse) current behaviour: a freg protected-address
        // wipe writes no EventCursor, so a later estate-update event is processed normally and
        // re-grants the roles the wipe removed. If protected-address removal is meant to be
        // durable, this test should start failing - which is the signal we want.
        var estateSsn = _databaseFixture.NextSsn;
        var heirSsn = _databaseFixture.NextSsn;
        var firstTimestamp = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var laterTimestamp = firstTimestamp.AddHours(2);

        EstateCaseUpdatedEvent BuildEstateEventData() => new()
        {
            Time = DateTimeOffset.UtcNow,
            ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
            CaseNumber = "abc123",
            DistrictCourtName = "Oslo tingrett",
            CaseId = Guid.NewGuid().ToString(),
            CaseStatus = CaseStatus.Mottatt,
            HeirRolesV2 = [
                new PersonHeirRole
                {
                    Nin = heirSsn,
                    Role = Constants.FormuesfullmaktRoleCode,
                    Relation = HeirRoleRelation.Barn,
                }
            ]
        };

        // Arrange: estate event grants the heir a court role
        var firstEstateEvent = new CloudEvent
        {
            Time = firstTimestamp,
            Type = EventType.CaseStatusUpdateValidated,
            Subject = estateSsn,
            Data = JsonSerializer.Serialize(BuildEstateEventData())
        };

        await _sut.HandleEvent(firstEstateEvent);

        _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList()
            .Should().ContainSingle(ra =>
                ra.RecipientSsn == heirSsn && ra.RoleCode == Constants.FormuesfullmaktRoleCode);

        // Freg protected-address update wipes the estate (and advances no estate EventCursor)
        var fregEvent = new CloudEvent
        {
            Time = firstTimestamp.AddHours(1),
            Type = EventType.FregProtectedAddressUpdate,
            Subject = heirSsn,
            Data = JsonSerializer.Serialize(new { nin = heirSsn })
        };

        await _sut.HandleEvent(fregEvent);

        _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList()
            .Should().BeEmpty();

        // Act: a later estate sync for the same estate
        var secondEstateEvent = new CloudEvent
        {
            Time = laterTimestamp,
            Type = EventType.CaseStatusUpdateValidated,
            Subject = estateSsn,
            Data = JsonSerializer.Serialize(BuildEstateEventData())
        };

        await _sut.HandleEvent(secondEstateEvent);

        // Assert: the court role is back - the freg wipe was not durable against a later court sync.
        _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList()
            .Should().ContainSingle(ra =>
                ra.RecipientSsn == heirSsn && ra.RoleCode == Constants.FormuesfullmaktRoleCode);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
}