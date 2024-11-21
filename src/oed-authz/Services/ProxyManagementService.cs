using System.Text.Json;
using oed_authz.Interfaces;
using oed_authz.Models;
using oed_authz.Settings;

namespace oed_authz.Services;

public class ProxyManagementService : IProxyManagementService
{
    private readonly IRoleAssignmentsRepository _oedRoleRepositoryService;
    private readonly ILogger<ProxyManagementService> _logger;

    public ProxyManagementService(
        IRoleAssignmentsRepository oedRoleRepositoryService,
        ILogger<ProxyManagementService> logger)
    {
        _oedRoleRepositoryService = oedRoleRepositoryService;
        _logger = logger;
    }

    public async Task Add(ProxyManagementRequest proxyManagementRequest)
    {
        await ValidateRequest(proxyManagementRequest);

        var roleAssignment = new RoleAssignment
        {
            EstateSsn = proxyManagementRequest.EstateSsn,
            HeirSsn = proxyManagementRequest.ProxyRoleAssignment.HeirSsn,
            RecipientSsn = proxyManagementRequest.ProxyRoleAssignment.RecipientSsn,
            RoleCode = proxyManagementRequest.ProxyRoleAssignment.RoleCode,
            Created = proxyManagementRequest.ProxyRoleAssignment.Created ?? DateTimeOffset.Now
        };

        await _oedRoleRepositoryService.AddRoleAssignment(roleAssignment);
        await UpdateProxyRoleAssigments(proxyManagementRequest.EstateSsn);
    }

    public async Task Remove(ProxyManagementRequest proxyManagementRequest)
    {
        await ValidateRequest(proxyManagementRequest);

        var roleAssignment = new RoleAssignment
        {
            EstateSsn = proxyManagementRequest.EstateSsn,
            HeirSsn =  proxyManagementRequest.ProxyRoleAssignment.HeirSsn,
            RecipientSsn = proxyManagementRequest.ProxyRoleAssignment.RecipientSsn,
            RoleCode = proxyManagementRequest.ProxyRoleAssignment.RoleCode
        };

        await _oedRoleRepositoryService.RemoveRoleAssignment(roleAssignment);
        await UpdateProxyRoleAssigments(proxyManagementRequest.EstateSsn);
    }

    public async Task UpdateProxyRoleAssigments(string estateSsn)
    {
        // Check if all heirs within the estate with the probate role have given a proxy role to the same recipient
        // If so, that recipient should be given the collective proxy role from the estate it self.
        var roleAssignmentsForEstate = await _oedRoleRepositoryService.GetRoleAssignmentsForEstate(estateSsn);

        var heirsWithProbateRole = roleAssignmentsForEstate
            .Where(x => x.RoleCode == Constants.ProbateRoleCode)
            .ToList();

        var individualProxyAssigments = roleAssignmentsForEstate
            .Where(x => x.RoleCode == Constants.IndividualProxyRoleCode)
            .ToList();

        // If there are any individual proxy assignments from heirs without probate role, those assignments are no longer valid and must be removed
        var invalidIndividualProxyAssignments = individualProxyAssigments
            .Where(x => heirsWithProbateRole
                .All(y => y.RecipientSsn != x.HeirSsn))
            .ToList();

        foreach (var invalidIndividualProxyAssignment in invalidIndividualProxyAssignments)
        {
            _logger.LogInformation("Removing no longer valid individual proxy assignment: {InvalidIndividualProxyAssignment}",
                JsonSerializer.Serialize(invalidIndividualProxyAssignment));

            await _oedRoleRepositoryService.RemoveRoleAssignment(
                new RoleAssignment
                {
                    EstateSsn = estateSsn,
                    HeirSsn = invalidIndividualProxyAssignment.HeirSsn,
                    RecipientSsn = invalidIndividualProxyAssignment.RecipientSsn,
                });
        }
        // Update the list of individual proxy assignments to only contain valid assignments
        individualProxyAssigments = individualProxyAssigments
            .Where(x => heirsWithProbateRole
                .Any(y => y.RecipientSsn == x.HeirSsn))
            .ToList();

        var collectiveProxyAssigments = roleAssignmentsForEstate
            .Where(x => x.RoleCode == Constants.CollectiveProxyRoleCode)
            .ToList();

        var eligibleCollectiveProxyRecipients = GetEligibleCollectiveProxyRecipients(heirsWithProbateRole, individualProxyAssigments);

        // Find the eligible recipients that does not already have the collective proxy role
        var eligibleRecipientsWithoutCollectiveProxyRole = eligibleCollectiveProxyRecipients
            .Where(x => collectiveProxyAssigments
                .All(y => y.RecipientSsn != x))
            .ToList();

        // Grant the collective proxy role to the eligible recipients
        foreach (var eligibleRecipient in eligibleRecipientsWithoutCollectiveProxyRole)
        {
            _logger.LogInformation("Granting collective proxy role to eligible recipient: {EligibleRecipient}", eligibleRecipient);
            await _oedRoleRepositoryService.AddRoleAssignment(
                new RoleAssignment
                {
                    EstateSsn = estateSsn,
                    RecipientSsn = eligibleRecipient,
                    RoleCode = Constants.CollectiveProxyRoleCode,
                    Created = DateTimeOffset.UtcNow
                });
        }

        // Find any recipients that have the collective proxy role, but should not have it anymore
        var noLongerEligbleRecipientsWithCollectiveProxyRole = collectiveProxyAssigments
            .Where(x => !eligibleCollectiveProxyRecipients
                .Contains(x.RecipientSsn))
            .ToList();

        // Revoke the collective proxy role from the recipients that should no longer have it
        foreach (var noLongerEligbleRecipient in noLongerEligbleRecipientsWithCollectiveProxyRole)
        {
            _logger.LogInformation("Revoking collective proxy role from no longer eligible recipient: {NoLongerEligbleRecipient}",
                JsonSerializer.Serialize(noLongerEligbleRecipient));

            await _oedRoleRepositoryService.RemoveRoleAssignment(
                new RoleAssignment
                {
                    EstateSsn = estateSsn,
                    RecipientSsn = noLongerEligbleRecipient.RecipientSsn,
                    RoleCode = Constants.CollectiveProxyRoleCode
                });
        }
    }

    private async Task ValidateRequest(ProxyManagementRequest proxyManagementRequest)
    {
        if (!proxyManagementRequest.ProxyRoleAssignment.RoleCode.Equals(Constants.IndividualProxyRoleCode))
        {
            throw new ArgumentException("Can only assign " + Constants.IndividualProxyRoleCode);
        }

        if (!Utils.IsValidSsn(proxyManagementRequest.EstateSsn))
        {
            throw new ArgumentException(nameof(proxyManagementRequest.ProxyRoleAssignment.RecipientSsn));
        }

        if (!Utils.IsValidSsn(proxyManagementRequest.ProxyRoleAssignment.RecipientSsn))
        {
            throw new ArgumentException(nameof(proxyManagementRequest.ProxyRoleAssignment.RecipientSsn));
        }

        if (!Utils.IsValidSsn(proxyManagementRequest.ProxyRoleAssignment.HeirSsn))
        {
            throw new ArgumentException(nameof(proxyManagementRequest.ProxyRoleAssignment.HeirSsn));
        }

        // We need to check if "from" has the probate role for the given estate
        var heirRoles = await _oedRoleRepositoryService.GetRoleAssignmentsForPerson(
            proxyManagementRequest.EstateSsn, proxyManagementRequest.ProxyRoleAssignment.HeirSsn);

        if (heirRoles.All(ra => ra.RoleCode != Constants.ProbateRoleCode))
        {
            throw new InvalidOperationException("The heir does not have the probate role for the given estate");
        }
    }

    private static List<string> GetEligibleCollectiveProxyRecipients(
        List<RoleAssignment> heirRoleAssignments,
        List<RoleAssignment> individualProxyRoleAssignments)
    {
        // Get the recipients that have been given a proxy role by all heir with the probate role
        var heirsWithProbateRoles = heirRoleAssignments
            .Select(x => x.RecipientSsn)
            .Distinct()
            .ToArray();

        var eligibleCollectiveProxyRecipients = individualProxyRoleAssignments
            .GroupBy(x => x.RecipientSsn)
            .Where(x => x.Count() == heirsWithProbateRoles.Length)
            .Select(x => x.Key)
            .ToList();

        // Heirs with probate roles may also be assigned individual proxy roles from other heirs with probate roles
        // In which case, they should also be eligble for the collective proxy role if all heirs with probate roles
        // other than themselves have given them a proxy role
        foreach (var heirWithProbateRole in heirsWithProbateRoles)
        {
            var otherHeirsWithProbateRoles = heirsWithProbateRoles
                .Where(x => x != heirWithProbateRole)
                .ToArray();

            var otherHeirsWithProbateRolesThatHaveGivenProxyRoleToHeir = individualProxyRoleAssignments
                .Where(x =>
                    x.RecipientSsn == heirWithProbateRole
                    && otherHeirsWithProbateRoles.Contains(x.HeirSsn!))
                .Select(x => x.HeirSsn)
                .Distinct()
                .ToArray();

            if (otherHeirsWithProbateRolesThatHaveGivenProxyRoleToHeir.Length == otherHeirsWithProbateRoles.Length)
            {
                eligibleCollectiveProxyRecipients.Add(heirWithProbateRole);
            }
        }

        return eligibleCollectiveProxyRecipients;
    }
}
