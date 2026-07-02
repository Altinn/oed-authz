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

    [Fact]
    public async Task HandleEvent_FregProtectedAddressUpdate_WhenProtectedPersonIsOnlyAProxyRecipient_ShouldRemoveOnlyThatProxy_AndLeaveEstateIntact()
    {
        // Arrange - the protected person is NOT an heir in this estate. Two heirs hold the
        // probate role, and the protected person has merely received an individual proxy from
        // one of them. Per the rule in HandleProtectedAddressUpdate ("Affected nin is a proxy
        // recipient, only remove the proxy role"), only that single delegation is revoked - the
        // estate belongs to the heirs and must remain intact.
        var protectedProxyRecipientSsn = _databaseFixture.NextSsn;
        var heirWhoDelegated = _databaseFixture.NextSsn;
        var otherHeir = _databaseFixture.NextSsn;
        var estateSsn = _databaseFixture.NextSsn;

        _dbContext.RoleAssignments.AddRange(
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = heirWhoDelegated,
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = otherHeir,
                RoleCode = Constants.ProbateRoleCode,
            },
            // The protected person received an individual proxy from one of the two heirs.
            // Receiving a proxy from only one of two probate heirs means no collective proxy exists.
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = protectedProxyRecipientSsn,
                HeirSsn = heirWhoDelegated,
                RoleCode = Constants.IndividualProxyRoleCode,
            });

        await _dbContext.SaveChangesAsync();

        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.UtcNow,
            Type = EventType.FregProtectedAddressUpdate,
            Subject = protectedProxyRecipientSsn,
            Data = JsonSerializer.Serialize(new { nin = protectedProxyRecipientSsn })
        };

        // Act
        await _sut.HandleEvent(cloudEvent);

        // Assert - only the proxy pointing at the protected person is gone. Both heirs keep
        // their probate roles, and no collective proxy is incorrectly added or removed.
        var remaining = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        remaining.Should().HaveCount(2);
        remaining.Should().ContainSingle(ra =>
            ra.RecipientSsn == heirWhoDelegated && ra.RoleCode == Constants.ProbateRoleCode);
        remaining.Should().ContainSingle(ra =>
            ra.RecipientSsn == otherHeir && ra.RoleCode == Constants.ProbateRoleCode);
        remaining.Should().NotContain(ra => ra.RecipientSsn == protectedProxyRecipientSsn);
        remaining.Should().NotContain(ra => ra.RoleCode == Constants.IndividualProxyRoleCode);
    }

    [Fact]
    public async Task HandleEvent_FregProtectedAddressUpdate_WhenProtectedPersonIsAnHeirWithCourtRole_ShouldWipeEntireEstate()
    {
        // Arrange - the protected person is an heir (holds a court-assigned role). Protecting
        // them requires wiping the whole estate, including other heirs and any proxy assignments.
        var protectedHeirSsn = _databaseFixture.NextSsn;
        var otherHeirSsn = _databaseFixture.NextSsn;
        var proxyRecipientSsn = _databaseFixture.NextSsn;
        var estateSsn = _databaseFixture.NextSsn;

        _dbContext.RoleAssignments.AddRange(
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = protectedHeirSsn,
                RoleCode = Constants.FormuesfullmaktRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = otherHeirSsn,
                RoleCode = Constants.ProbateRoleCode,
            },
            // A proxy that does not involve the protected person - it must still be wiped
            // because the whole estate goes when a heir is protected.
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = proxyRecipientSsn,
                HeirSsn = otherHeirSsn,
                RoleCode = Constants.IndividualProxyRoleCode,
            });

        await _dbContext.SaveChangesAsync();

        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.UtcNow,
            Type = EventType.FregProtectedAddressUpdate,
            Subject = protectedHeirSsn,
            Data = JsonSerializer.Serialize(new { nin = protectedHeirSsn })
        };

        // Act
        await _sut.HandleEvent(cloudEvent);

        // Assert - the entire estate is gone, not just the protected person's own role.
        _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task HandleEvent_FregProtectedAddressUpdate_ShouldWipeEstatesWhereHeir_RemoveOnlyProxyWhereDelegate_AndLeaveUnrelatedEstatesUntouched()
    {
        // A single protected person appears in three estates in three different capacities.
        // This locks down that the two rules are applied per role assignment, independently,
        // and that unrelated estates are never touched.
        var protectedPersonSsn = _databaseFixture.NextSsn;

        // Estate X: protected person is an heir -> the whole estate must be wiped.
        var estateWhereHeir = _databaseFixture.NextSsn;
        var coHeirInX = _databaseFixture.NextSsn;

        // Estate Y: protected person is only a proxy recipient -> only that proxy is removed.
        var estateWhereDelegate = _databaseFixture.NextSsn;
        var heir1InY = _databaseFixture.NextSsn;
        var heir2InY = _databaseFixture.NextSsn;

        // Estate Z: protected person not involved -> must be left untouched.
        var unrelatedEstate = _databaseFixture.NextSsn;
        var heirInZ = _databaseFixture.NextSsn;

        _dbContext.RoleAssignments.AddRange(
            // Estate X
            new RoleAssignment
            {
                EstateSsn = estateWhereHeir,
                RecipientSsn = protectedPersonSsn,
                RoleCode = Constants.FormuesfullmaktRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateWhereHeir,
                RecipientSsn = coHeirInX,
                RoleCode = Constants.FormuesfullmaktRoleCode,
            },
            // Estate Y
            new RoleAssignment
            {
                EstateSsn = estateWhereDelegate,
                RecipientSsn = heir1InY,
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateWhereDelegate,
                RecipientSsn = heir2InY,
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateWhereDelegate,
                RecipientSsn = protectedPersonSsn,
                HeirSsn = heir1InY,
                RoleCode = Constants.IndividualProxyRoleCode,
            },
            // Estate Z
            new RoleAssignment
            {
                EstateSsn = unrelatedEstate,
                RecipientSsn = heirInZ,
                RoleCode = Constants.FormuesfullmaktRoleCode,
            });

        await _dbContext.SaveChangesAsync();

        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.UtcNow,
            Type = EventType.FregProtectedAddressUpdate,
            Subject = protectedPersonSsn,
            Data = JsonSerializer.Serialize(new { nin = protectedPersonSsn })
        };

        // Act
        await _sut.HandleEvent(cloudEvent);

        // Assert - Estate X wiped entirely
        _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateWhereHeir)
            .ToList()
            .Should().BeEmpty();

        // Estate Y: only the proxy to the protected person removed; both heirs' roles intact
        var estateY = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateWhereDelegate)
            .ToList();

        estateY.Should().HaveCount(2);
        estateY.Should().ContainSingle(ra =>
            ra.RecipientSsn == heir1InY && ra.RoleCode == Constants.ProbateRoleCode);
        estateY.Should().ContainSingle(ra =>
            ra.RecipientSsn == heir2InY && ra.RoleCode == Constants.ProbateRoleCode);
        estateY.Should().NotContain(ra => ra.RecipientSsn == protectedPersonSsn);

        // Estate Z: completely untouched
        _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == unrelatedEstate)
            .ToList()
            .Should().ContainSingle(ra =>
                ra.RecipientSsn == heirInZ && ra.RoleCode == Constants.FormuesfullmaktRoleCode);
    }

    [Fact]
    public async Task HandleEvent_FregProtectedAddressUpdate_WhenPersonHasNoRoleAssignments_ShouldDoNothing()
    {
        // Arrange - a protected-address event for someone with no role assignments at all
        // must be a safe no-op and must not affect any other estate.
        var personWithNoRolesSsn = _databaseFixture.NextSsn;
        var unrelatedEstate = _databaseFixture.NextSsn;
        var heirSsn = _databaseFixture.NextSsn;

        _dbContext.RoleAssignments.Add(
            new RoleAssignment
            {
                EstateSsn = unrelatedEstate,
                RecipientSsn = heirSsn,
                RoleCode = Constants.FormuesfullmaktRoleCode,
            });

        await _dbContext.SaveChangesAsync();

        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.UtcNow,
            Type = EventType.FregProtectedAddressUpdate,
            Subject = personWithNoRolesSsn,
            Data = JsonSerializer.Serialize(new { nin = personWithNoRolesSsn })
        };

        // Act
        var act = async () => await _sut.HandleEvent(cloudEvent);

        // Assert
        await act.Should().NotThrowAsync();

        _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == unrelatedEstate)
            .ToList()
            .Should().ContainSingle(ra => ra.RecipientSsn == heirSsn);
    }

    [Fact]
    public async Task HandleEvent_FregProtectedAddressUpdate_WithNullData_ShouldThrowArgumentNullException()
    {
        // Arrange - a freg event with no data payload should be rejected before any DB work.
        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.UtcNow,
            Type = EventType.FregProtectedAddressUpdate,
            Subject = _databaseFixture.NextSsn,
            Data = null!
        };

        // Act
        var act = async () => await _sut.HandleEvent(cloudEvent);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleEvent_FregProtectedAddressUpdate_WithJsonNullData_ShouldReturnWithoutRemovingAnything()
    {
        // Arrange - a payload that deserializes to null (JSON literal null) must be treated as
        // a safe no-op: the handler returns early and touches no role assignments.
        var estateSsn = _databaseFixture.NextSsn;
        var heirSsn = _databaseFixture.NextSsn;

        _dbContext.RoleAssignments.Add(
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = heirSsn,
                RoleCode = Constants.FormuesfullmaktRoleCode,
            });

        await _dbContext.SaveChangesAsync();

        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.UtcNow,
            Type = EventType.FregProtectedAddressUpdate,
            Subject = heirSsn,
            Data = "null" // deserializes to null -> handler returns early
        };

        // Act
        await _sut.HandleEvent(cloudEvent);

        // Assert
        _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList()
            .Should().ContainSingle(ra => ra.RecipientSsn == heirSsn);
    }

    [Fact]
    public async Task HandleEvent_FregProtectedAddressUpdate_WhenProtectedPersonIsANonHeirHoldingCollectiveProxy_ShouldRemoveOnlyTheirProxies_AndLeaveEstateIntact()
    {
        // Arrange - the protected person is NOT an heir. They received an individual proxy from
        // BOTH probate heirs and therefore also hold the auto-managed collective proxy. Only that
        // person's proxy access (individual + collective) should be revoked - the heirs keep their
        // own court roles and the estate must stay intact.
        var estateSsn = _databaseFixture.NextSsn;
        var heir1 = _databaseFixture.NextSsn;
        var heir2 = _databaseFixture.NextSsn;
        var nonHeirDelegate = _databaseFixture.NextSsn;

        _dbContext.RoleAssignments.AddRange(
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = heir1,
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = heir2,
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = nonHeirDelegate,
                HeirSsn = heir1,
                RoleCode = Constants.IndividualProxyRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = nonHeirDelegate,
                HeirSsn = heir2,
                RoleCode = Constants.IndividualProxyRoleCode,
            },
            // Auto-managed collective proxy the delegate holds because they received a proxy from
            // every probate heir. Note it has no HeirSsn - the row that used to trigger a full wipe.
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = nonHeirDelegate,
                RoleCode = Constants.CollectiveProxyRoleCode,
            });

        await _dbContext.SaveChangesAsync();

        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.UtcNow,
            Type = EventType.FregProtectedAddressUpdate,
            Subject = nonHeirDelegate,
            Data = JsonSerializer.Serialize(new { nin = nonHeirDelegate })
        };

        // Act
        await _sut.HandleEvent(cloudEvent);

        // Assert - both heirs keep their probate roles; every trace of the delegate's proxy
        // access (individual and collective) is gone. The estate is NOT wiped.
        var remaining = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList();

        remaining.Should().HaveCount(2);
        remaining.Should().ContainSingle(ra =>
            ra.RecipientSsn == heir1 && ra.RoleCode == Constants.ProbateRoleCode);
        remaining.Should().ContainSingle(ra =>
            ra.RecipientSsn == heir2 && ra.RoleCode == Constants.ProbateRoleCode);
        remaining.Should().NotContain(ra => ra.RecipientSsn == nonHeirDelegate);
    }

    [Fact]
    public async Task HandleEvent_FregProtectedAddressUpdate_WhenPersonIsAProxyRecipientInMultipleEstates_ShouldRemoveOnlyTheirProxiesInEach_RegardlessOfHeirCount()
    {
        // The protected person is a NON-heir delegate in two different estates at once:
        //  - Estate S has a single probate heir.
        //  - Estate M has two probate heirs.
        // In both, the delegate received a proxy from every heir and therefore also holds the
        // auto-managed collective proxy. The freg event must revoke only the delegate's proxy
        // access in each estate, leaving each estate's own heirs untouched.
        var delegateSsn = _databaseFixture.NextSsn;

        // Estate S - single heir
        var estateS = _databaseFixture.NextSsn;
        var soleHeir = _databaseFixture.NextSsn;

        // Estate M - multiple heirs
        var estateM = _databaseFixture.NextSsn;
        var heirM1 = _databaseFixture.NextSsn;
        var heirM2 = _databaseFixture.NextSsn;

        _dbContext.RoleAssignments.AddRange(
            // Estate S: the sole probate heir (who, being the only heir, also holds a collective
            // proxy), plus the delegate's individual + collective proxy.
            new RoleAssignment { EstateSsn = estateS, RecipientSsn = soleHeir, RoleCode = Constants.ProbateRoleCode },
            new RoleAssignment { EstateSsn = estateS, RecipientSsn = soleHeir, RoleCode = Constants.CollectiveProxyRoleCode },
            new RoleAssignment { EstateSsn = estateS, RecipientSsn = delegateSsn, HeirSsn = soleHeir, RoleCode = Constants.IndividualProxyRoleCode },
            new RoleAssignment { EstateSsn = estateS, RecipientSsn = delegateSsn, RoleCode = Constants.CollectiveProxyRoleCode },
            // Estate M: two probate heirs and the delegate's two individual proxies + collective proxy.
            new RoleAssignment { EstateSsn = estateM, RecipientSsn = heirM1, RoleCode = Constants.ProbateRoleCode },
            new RoleAssignment { EstateSsn = estateM, RecipientSsn = heirM2, RoleCode = Constants.ProbateRoleCode },
            new RoleAssignment { EstateSsn = estateM, RecipientSsn = delegateSsn, HeirSsn = heirM1, RoleCode = Constants.IndividualProxyRoleCode },
            new RoleAssignment { EstateSsn = estateM, RecipientSsn = delegateSsn, HeirSsn = heirM2, RoleCode = Constants.IndividualProxyRoleCode },
            new RoleAssignment { EstateSsn = estateM, RecipientSsn = delegateSsn, RoleCode = Constants.CollectiveProxyRoleCode });

        await _dbContext.SaveChangesAsync();

        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.UtcNow,
            Type = EventType.FregProtectedAddressUpdate,
            Subject = delegateSsn,
            Data = JsonSerializer.Serialize(new { nin = delegateSsn })
        };

        // Act
        await _sut.HandleEvent(cloudEvent);

        // Assert - Estate S (single heir): only the sole heir's rows remain, the delegate is gone.
        var estateSRemaining = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateS)
            .ToList();

        estateSRemaining.Should().OnlyContain(ra => ra.RecipientSsn == soleHeir);
        estateSRemaining.Should().Contain(ra => ra.RoleCode == Constants.ProbateRoleCode);
        estateSRemaining.Should().NotContain(ra => ra.RecipientSsn == delegateSsn);

        // Estate M (multiple heirs): both heirs keep their probate roles, the delegate is gone.
        var estateMRemaining = _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateM)
            .ToList();

        estateMRemaining.Should().HaveCount(2);
        estateMRemaining.Should().ContainSingle(ra => ra.RecipientSsn == heirM1 && ra.RoleCode == Constants.ProbateRoleCode);
        estateMRemaining.Should().ContainSingle(ra => ra.RecipientSsn == heirM2 && ra.RoleCode == Constants.ProbateRoleCode);
        estateMRemaining.Should().NotContain(ra => ra.RecipientSsn == delegateSsn);
    }

    [Fact]
    public async Task HandleEvent_FregProtectedAddressUpdate_WhenProtectedPersonIsTheSoleHeir_ShouldWipeEntireEstate()
    {
        // A single-heir estate where the protected person IS that heir. The whole estate must be
        // wiped, including the collective proxy the sole heir automatically holds.
        var soleHeirSsn = _databaseFixture.NextSsn;
        var estateSsn = _databaseFixture.NextSsn;

        _dbContext.RoleAssignments.AddRange(
            new RoleAssignment { EstateSsn = estateSsn, RecipientSsn = soleHeirSsn, RoleCode = Constants.ProbateRoleCode },
            new RoleAssignment { EstateSsn = estateSsn, RecipientSsn = soleHeirSsn, RoleCode = Constants.CollectiveProxyRoleCode });

        await _dbContext.SaveChangesAsync();

        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.UtcNow,
            Type = EventType.FregProtectedAddressUpdate,
            Subject = soleHeirSsn,
            Data = JsonSerializer.Serialize(new { nin = soleHeirSsn })
        };

        // Act
        await _sut.HandleEvent(cloudEvent);

        // Assert
        _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public async Task HandleEvent_FregProtectedAddressUpdate_WhenPersonIsBothHeirAndProxyRecipientInSameEstate_ShouldWipeEstate_WithoutError()
    {
        // The protected person is a probate heir AND has received an individual proxy from a
        // co-heir in the SAME estate (a supported state - see GetEligibleCollectiveProxyRecipients).
        // Because they are an heir, the whole estate must be wiped, and the operation must not
        // depend on the order the person's rows happen to come back from the database.
        var estateSsn = _databaseFixture.NextSsn;
        var protectedHeirSsn = _databaseFixture.NextSsn;
        var coHeirSsn = _databaseFixture.NextSsn;

        _dbContext.RoleAssignments.AddRange(
            // Court role for the protected person (this row drives the full-estate wipe).
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = protectedHeirSsn,
                RoleCode = Constants.ProbateRoleCode,
            },
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = coHeirSsn,
                RoleCode = Constants.ProbateRoleCode,
            },
            // ...and the protected person is also a proxy recipient from the co-heir.
            new RoleAssignment
            {
                EstateSsn = estateSsn,
                RecipientSsn = protectedHeirSsn,
                HeirSsn = coHeirSsn,
                RoleCode = Constants.IndividualProxyRoleCode,
            });

        await _dbContext.SaveChangesAsync();

        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.UtcNow,
            Type = EventType.FregProtectedAddressUpdate,
            Subject = protectedHeirSsn,
            Data = JsonSerializer.Serialize(new { nin = protectedHeirSsn })
        };

        // Act
        var act = async () => await _sut.HandleEvent(cloudEvent);

        // Assert - must not throw, and the estate must be fully wiped.
        await act.Should().NotThrowAsync();

        _dbContext.RoleAssignments
            .Where(ra => ra.EstateSsn == estateSsn)
            .ToList()
            .Should().BeEmpty();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
}
