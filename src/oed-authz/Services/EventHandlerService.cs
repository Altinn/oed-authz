using System.Text.Json;
using oed_authz.Infrastructure.Database;
using oed_authz.Infrastructure.Database.Model;
using oed_authz.Interfaces;
using oed_authz.Models;
using oed_authz.Models.Dto;
using oed_authz.Settings;
using oed_authz.Utils;

namespace oed_authz.Services;

public class AltinnEventHandlerService(
    OedAuthzDbContext dbContext,
    IEventCursorRepository eventCursorRepository,
    IRoleAssignmentsRepository oedRoleRepositoryService,
    IProxyManagementService proxyManagementService,
    ILogger<AltinnEventHandlerService> logger)
    : IAltinnEventHandlerService
{
    public Task HandleEvent(CloudEvent cloudEvent)
    {
        return cloudEvent.Type switch
        {
            Events.Oed.CaseStatusUpdateValidated => HandleEstateInstanceCreatedOrUpdated(cloudEvent),
            Events.Platform.ValidateSubscription => Task.CompletedTask,
            _ => throw new ArgumentException("Unknown event type")
        };
    }

    private async Task HandleEstateInstanceCreatedOrUpdated(CloudEvent daEvent)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var eventCursor = await GetOrCreateEventCursorForUpdate(daEvent);
        
        // Discard event if out of order
        if (EventIsOutOfOrder(eventCursor, daEvent))
        {
            logger.LogInformation(
                "Discarding event {Id} of type {EventType} for estate {Estate} - event is out of order",
                daEvent.Id, 
                daEvent.Type,
                daEvent.Subject);

            return;
        }

        if (daEvent.Data == null)
        {
            logger.LogError("Empty data in event: {CloudEvent}", JsonSerializer.Serialize(daEvent));
            throw new ArgumentNullException(nameof(daEvent.Data));
        }

        var eventRoleAssignments = JsonSerializer.Deserialize<EventRoleAssignmentDataDto>(daEvent.Data.ToString()!)!;
        logger.LogInformation("Handling event {Id} for DA caseId: {DaCaseId}", daEvent.Id, eventRoleAssignments?.DaCaseId);

        var estateSsn = SsnUtils.GetEstateSsnFromCloudEvent(daEvent);

        if (eventRoleAssignments.CaseStatus == CaseStatus.FEILFORT)
        {
            await RemoveAllRoleAssignmentsForEstate(estateSsn, daEvent.Id);
        }
        else
        {
            await UpdateCourtAssignedRoleAssignments(estateSsn, eventRoleAssignments, daEvent.Id, daEvent.Time);
            // Handle collective proxy roles
            await proxyManagementService.UpdateProxyRoleAssigments(estateSsn);
        }

        eventCursor.LastTimestampProcessed = daEvent.Time.ToUniversalTime();
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task UpdateCourtAssignedRoleAssignments(
        string estateSsn, 
        EventRoleAssignmentDataDto eventRoleAssignments, 
        string eventId,
        DateTimeOffset eventTime)
    {
        // Get all current court assigned roles from this estate
        var currentCourtAssignedRoleAssignments = (await oedRoleRepositoryService.GetRoleAssignmentsForEstate(estateSsn))
            .Where(x => x.RoleCode.StartsWith(Constants.CourtRoleCodePrefix))
            .ToList();
        
        // Find assignments in updated list but not in current list to add
        var assignmentsToAdd = new List<RoleAssignment>();
        foreach (var updatedRoleAssignment in eventRoleAssignments.HeirRoles)
        {
            if (!SsnUtils.IsValidSsn(updatedRoleAssignment.Nin))
            {
                throw new ArgumentException(nameof(updatedRoleAssignment.Nin));
            }

            // Check that all role codes are within the correct namespace
            if (!updatedRoleAssignment.Role.StartsWith(Constants.CourtRoleCodePrefix))
            {
                throw new ArgumentException("Rolecode must start with " + Constants.CourtRoleCodePrefix);
            }

            if (!currentCourtAssignedRoleAssignments
                .Exists(x => 
                    x.RecipientSsn == updatedRoleAssignment.Nin && 
                    x.RoleCode == updatedRoleAssignment.Role))
            {
                assignmentsToAdd.Add(new RoleAssignment
                {
                    EstateSsn = estateSsn,
                    RecipientSsn = updatedRoleAssignment.Nin,
                    RoleCode = updatedRoleAssignment.Role,
                    Created = eventTime.ToUniversalTime()
                });
            }
        }

        // Find assignments in current list that's not in the updated list. These should be removed.
        var assignmentsToRemove = new List<RoleAssignment>();
        foreach (var currentRoleAssignment in currentCourtAssignedRoleAssignments)
        {
            if (!eventRoleAssignments.HeirRoles
                .Exists(x =>
                    x.Nin == currentRoleAssignment.RecipientSsn && 
                    x.Role == currentRoleAssignment.RoleCode))
            {
                assignmentsToRemove.Add(new RoleAssignment
                {
                    EstateSsn = estateSsn,
                    RecipientSsn = currentRoleAssignment.RecipientSsn,
                    RoleCode = currentRoleAssignment.RoleCode
                });
            }
        }

        logger.LogInformation("Handling event {Id}: {AssignmentsToAdd} assignments to add and {AssignmentsToRemove} assignments to remove",
            eventId, assignmentsToAdd.Count, assignmentsToRemove.Count);

        foreach (var roleAssignment in assignmentsToAdd)
        {
            await oedRoleRepositoryService.AddRoleAssignment(roleAssignment);
        }

        foreach (var roleAssignment in assignmentsToRemove)
        {
            await oedRoleRepositoryService.RemoveRoleAssignment(roleAssignment);
        }
    }

    private async Task RemoveAllRoleAssignmentsForEstate(string estateSsn, string eventId)
    {
        // Get all current roles from this estate
        var assignmentsToRemove = await oedRoleRepositoryService.GetRoleAssignmentsForEstate(estateSsn);

        logger.LogInformation("Handling event {Id}: Removing all assignments ({AssignmentsToRemove})",
            eventId, assignmentsToRemove.Count);

        foreach (var roleAssignment in assignmentsToRemove)
        {
            await oedRoleRepositoryService.RemoveRoleAssignment(roleAssignment);
        }
    }

    private static bool EventIsOutOfOrder(EventCursor cursor, CloudEvent cloudEvent)
    {
        return cursor.LastTimestampProcessed != default
               && cloudEvent.Time <= cursor.LastTimestampProcessed;
    }

    private async Task<EventCursor> GetOrCreateEventCursorForUpdate(CloudEvent daEvent)
    {
        var estateSsn = SsnUtils.GetEstateSsnFromCloudEvent(daEvent);
        var eventCursor = await eventCursorRepository.GetEventCursorForUpdate(estateSsn, daEvent.Type);

        if (eventCursor is not null)
            return eventCursor;

        eventCursor = new EventCursor
        {
            EstateSsn = estateSsn,
            EventType = daEvent.Type,
            LastTimestampProcessed = default
        };

        await eventCursorRepository.AddEventCursor(eventCursor);
        return eventCursor;
    }

}

public static class Events
{
    public static class Oed
    {
        public const string CaseStatusUpdateValidated = "no.altinn.events.digitalt-dodsbo.v1.case-status-update-validated";
    }

    public static class Platform
    {
        public const string ValidateSubscription = "platform.events.validatesubscription";
    }
}