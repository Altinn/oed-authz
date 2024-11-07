using System.Text.Json;
using oed_authz.Interfaces;
using oed_authz.Models;
using oed_authz.Models.Dto;
using oed_authz.Settings;

namespace oed_authz.Services;
public class AltinnEventHandlerService : IAltinnEventHandlerService
{
    private readonly IOedRoleRepositoryService _oedRoleRepositoryService;
    private readonly IProxyManagementService _proxyManagementService;
    private readonly ILogger<AltinnEventHandlerService> _logger;

    public AltinnEventHandlerService(
        IOedRoleRepositoryService oedRoleRepositoryService,
        IProxyManagementService proxyManagementService,
        ILogger<AltinnEventHandlerService> logger)
    {
        _oedRoleRepositoryService = oedRoleRepositoryService;
        _proxyManagementService = proxyManagementService;
        _logger = logger;
    }

    public async Task HandleEvent(CloudEvent cloudEvent)
    {
        switch (cloudEvent.Type)
        {
            case "no.altinn.events.digitalt-dodsbo.v1.case-status-updated":
                await HandleEstateInstanceCreatedOrUpdated(cloudEvent);
                break;
            case "no.altinn.events.digitalt-dodsbo.v1.heir-roles-updated":
                await HandleEstateInstanceCreatedOrUpdated(cloudEvent);
                break;
            case "platform.events.validatesubscription":
                return;
            default:
                throw new ArgumentException("Unknown event type");
        }
    }

    private async Task HandleEstateInstanceCreatedOrUpdated(CloudEvent daEvent)
    {
        if (daEvent.Data == null)
        {
            _logger.LogError("Empty data in event: {CloudEvent}", JsonSerializer.Serialize(daEvent));
            throw new ArgumentNullException(nameof(daEvent.Data));
        }

        _logger.LogInformation("Handling event {Id}: {CloudEvent}", daEvent.Id, JsonSerializer.Serialize(daEvent));

        var eventRoleAssignments = JsonSerializer.Deserialize<EventRoleAssignmentDataDto>(daEvent.Data.ToString()!)!;
        var estateSsn = Utils.GetEstateSsnFromCloudEvent(daEvent);

        if (eventRoleAssignments.CaseStatus == CaseStatus.FEILFORT)
        {
            await RemoveAllRoleAssignmentsForEstate(estateSsn, daEvent.Id);
        }
        else
        {
            await UpdateCourtAssignedRoleAssignments(estateSsn, eventRoleAssignments, daEvent.Id, daEvent.Time);
            // Handle collective proxy roles
            await _proxyManagementService.UpdateProxyRoleAssigments(estateSsn);
        }
    }

    private async Task UpdateCourtAssignedRoleAssignments(
        string estateSsn, 
        EventRoleAssignmentDataDto eventRoleAssignments, 
        string eventId,
        DateTimeOffset eventTime)
    {
        // Get all current court assigned roles from this estate
        var currentCourtAssignedRoleAssignments = (await _oedRoleRepositoryService.GetRoleAssignmentsForEstate(estateSsn))
            .Where(x => x.RoleCode.StartsWith(Constants.CourtRoleCodePrefix))
            .ToList();
        
        // Find assignments in updated list but not in current list to add
        var assignmentsToAdd = new List<RepositoryRoleAssignment>();
        foreach (var updatedRoleAssignment in eventRoleAssignments.HeirRoles)
        {
            if (!Utils.IsValidSsn(updatedRoleAssignment.Nin))
            {
                throw new ArgumentException(nameof(updatedRoleAssignment.Nin));
            }

            // Check if we have any current role assigments that are newer than this. If so, this means we're handling
            // an out-of-order and outdated event so we just bail.
            if (currentCourtAssignedRoleAssignments.Any(x => x.Created >= eventTime))
            {
                return;
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
                assignmentsToAdd.Add(new RepositoryRoleAssignment
                {
                    EstateSsn = estateSsn,
                    RecipientSsn = updatedRoleAssignment.Nin,
                    RoleCode = updatedRoleAssignment.Role,
                    Created = eventTime
                });
            }
        }

        // Find assignments in current list that's not in the updated list. These should be removed.
        var assignmentsToRemove = new List<RepositoryRoleAssignment>();
        foreach (var currentRoleAssignment in currentCourtAssignedRoleAssignments)
        {
            if (!eventRoleAssignments.HeirRoles
                .Exists(x =>
                    x.Nin == currentRoleAssignment.RecipientSsn && 
                    x.Role == currentRoleAssignment.RoleCode))
            {
                assignmentsToRemove.Add(new RepositoryRoleAssignment
                {
                    EstateSsn = estateSsn,
                    RecipientSsn = currentRoleAssignment.RecipientSsn,
                    RoleCode = currentRoleAssignment.RoleCode
                });
            }
        }

        _logger.LogInformation("Handling event {Id}: {AssignmentsToAdd} assignments to add and {AssignmentsToRemove} assignments to remove",
            eventId, assignmentsToAdd.Count, assignmentsToRemove.Count);

        foreach (var roleAssignment in assignmentsToAdd)
        {
            await _oedRoleRepositoryService.AddRoleAssignment(roleAssignment);
        }

        foreach (var roleAssignment in assignmentsToRemove)
        {
            await _oedRoleRepositoryService.RemoveRoleAssignment(roleAssignment);
        }
    }

    private async Task RemoveAllRoleAssignmentsForEstate(string estateSsn, string eventId)
    {
        // Get all current roles from this estate
        var assignmentsToRemove = await _oedRoleRepositoryService.GetRoleAssignmentsForEstate(estateSsn);

        _logger.LogInformation("Handling event {Id}: Removing all assignments ({AssignmentsToRemove})",
            eventId, assignmentsToRemove.Count);

        foreach (var roleAssignment in assignmentsToRemove)
        {
            await _oedRoleRepositoryService.RemoveRoleAssignment(roleAssignment);
        }
    }
}
