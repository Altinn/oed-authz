using System.Text.Json;
using FakeItEasy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using oed_authz.Infrastructure.Database;
using oed_authz.Infrastructure.Database.Model;
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

        var eventCursorRepo = new EventCursorRepository(_dbContext);

        var roleAssignmentsRepository = new RoleAssignmentsRepository(
            _dbContext,
            A.Fake<ILogger<RoleAssignmentsRepository>>());

        var proxyManagementService =
            new ProxyManagementService(roleAssignmentsRepository, A.Fake<ILogger<ProxyManagementService>>());

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
            Time = DateTimeOffset.Now,
            Type = Events.Oed.CaseStatusUpdateValidated,
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


        var eventRoleAssignments = new EventRoleAssignmentDataDto
        {
            DaCaseId = Guid.NewGuid().ToString(),
            CaseStatus = CaseStatus.FEILFORT,
        };

        var cloudEvent = new CloudEvent
        {
            Time = DateTimeOffset.Now,
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

    [Fact]
    public async Task HandleEvent_WithNoExistingEventCursor_ShouldInsertEventCursor_OnSuccess()
    {
        // Arrange
        var estateSsn = _databaseFixture.NextSsn;
        var daCaseId = Guid.NewGuid().ToString();
        var timestamp = new DateTimeOffset(2025, 8, 1, 18, 0, 0, TimeSpan.Zero);

        var roleAssignments = new EventRoleAssignmentDataDto
        {
            DaCaseId = daCaseId,
            CaseStatus = CaseStatus.MOTTATT,
            HeirRoles =
            [
                new EventRoleAssignmentDto
                {
                    Nin = "99999999991",
                    Role = Constants.FormuesfullmaktRoleCode
                }
            ]
        };

        var cloudEvent = new CloudEvent
        {
            Time = timestamp,
            Type = Events.Oed.CaseStatusUpdateValidated,
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

        var arrangeRoleAssignments = new EventRoleAssignmentDataDto
        {
            DaCaseId = daCaseId,
            CaseStatus = CaseStatus.MOTTATT,
            HeirRoles = []
        };

        var arrangeCloudEvent = new CloudEvent
        {
            Time = timestamp.Subtract(TimeSpan.FromSeconds(1)),
            Type = Events.Oed.CaseStatusUpdateValidated,
            Subject = $"person/{estateSsn}",
            Data = JsonSerializer.Serialize(arrangeRoleAssignments),
        };

        // First call to create cursor
        await _sut.HandleEvent(arrangeCloudEvent);

        // Verify preconditions
        var arrangeCursor = _dbContext.Set<EventCursor>().Single(c => c.EstateSsn == estateSsn);
        arrangeCursor.LastTimestampProcessed.Should().Be(timestamp.Subtract(TimeSpan.FromSeconds(1)));

        // Act
        var roleAssignments = new EventRoleAssignmentDataDto
        {
            DaCaseId = daCaseId,
            CaseStatus = CaseStatus.MOTTATT,
            HeirRoles =
            [
                new EventRoleAssignmentDto
                {
                    Nin = "99999999991",
                    Role = Constants.FormuesfullmaktRoleCode
                }
            ]
        };

        var cloudEvent = new CloudEvent
        {
            Time = timestamp,
            Type = Events.Oed.CaseStatusUpdateValidated,
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

        var arrangeRoleAssignments = new EventRoleAssignmentDataDto
        {
            DaCaseId = daCaseId,
            CaseStatus = CaseStatus.MOTTATT,
            HeirRoles = []
        };

        var arrangeCloudEvent = new CloudEvent
        {
            Time = timestamp.Subtract(TimeSpan.FromSeconds(1)),
            Type = Events.Oed.CaseStatusUpdateValidated,
            Subject = $"person/{estateSsn}",
            Data = JsonSerializer.Serialize(arrangeRoleAssignments),
        };

        // First call to create cursor
        await _sut.HandleEvent(arrangeCloudEvent);

        // Verify preconditions
        var arrangeCursor = _dbContext.Set<EventCursor>().Single(c => c.EstateSsn == estateSsn);
        arrangeCursor.LastTimestampProcessed.Should().Be(timestamp.Subtract(TimeSpan.FromSeconds(1)));

        // Act
        var roleAssignments = new EventRoleAssignmentDataDto
        {
            DaCaseId = daCaseId,
            CaseStatus = CaseStatus.MOTTATT,
            HeirRoles =
            [
                new EventRoleAssignmentDto
                {
                    Nin = "", // Should throws argument exception
                    Role = Constants.FormuesfullmaktRoleCode
                }
            ]
        };

        var cloudEvent = new CloudEvent
        {
            Time = timestamp,
            Type = Events.Oed.CaseStatusUpdateValidated,
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

        var roleAssignments = new EventRoleAssignmentDataDto
        {
            DaCaseId = daCaseId,
            CaseStatus = CaseStatus.MOTTATT,
            HeirRoles =
            [
                new EventRoleAssignmentDto
                {
                    Nin = "", // Should throw ArgumentException
                    Role = Constants.FormuesfullmaktRoleCode
                }
            ]
        };

        var cloudEvent = new CloudEvent
        {
            Time = timestamp,
            Type = Events.Oed.CaseStatusUpdateValidated,
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

        var latestRoleAssignments = new EventRoleAssignmentDataDto
        {
            DaCaseId = daCaseId,
            CaseStatus = CaseStatus.MOTTATT,
            HeirRoles =
            [
                new EventRoleAssignmentDto
                {
                    Nin = "99999999991",
                    Role = Constants.FormuesfullmaktRoleCode
                }
            ]
        };

        var latestEvent = new CloudEvent
        {
            Time = new DateTimeOffset(2025, 8, 1, 18, 0, 0, TimeSpan.Zero), // 2025-08-01T18:00:00+00
            Type = Events.Oed.CaseStatusUpdateValidated,
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
        var outOfOrderRoleAssignments = new EventRoleAssignmentDataDto
        {
            DaCaseId = daCaseId,
            CaseStatus = CaseStatus.MOTTATT,
            HeirRoles = []
        };

        var outOfOrderEvent = new CloudEvent
        {
            Time = new DateTimeOffset(2025, 8, 1, 17, 0, 0, TimeSpan.Zero), // 2025-08-01T17:00:00+00
            Type = Events.Oed.CaseStatusUpdateValidated,
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

        var roleAssignments = new EventRoleAssignmentDataDto
        {
            DaCaseId = daCaseId,
            CaseStatus = CaseStatus.MOTTATT,
            HeirRoles =
            [
                new EventRoleAssignmentDto
                {
                    Nin = "99999999991",
                    Role = Constants.FormuesfullmaktRoleCode
                }
            ]
        };

        var cloudEvent = new CloudEvent
        {
            Time = timestamp,
            Type = Events.Oed.CaseStatusUpdateValidated,
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
        var anotherRoleAssignments = new EventRoleAssignmentDataDto
        {
            DaCaseId = daCaseId,
            CaseStatus = CaseStatus.MOTTATT,
            HeirRoles = []
        };

        var anotherEvent = new CloudEvent
        {
            Time = timestamp, // Same timestamp as the previus event
            Type = Events.Oed.CaseStatusUpdateValidated,
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

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
}