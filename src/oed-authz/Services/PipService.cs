using oed_authz.Interfaces;
using oed_authz.Models;
using oed_authz.Settings;

namespace oed_authz.Services;
public class PipService(IRoleAssignmentsRepository repository) 
    : IPolicyInformationPointService
{
    public async Task<PipResponse> HandlePipRequest(PipRequest pipRequest)
    {
        // EstateSsn is required for now
        if (string.IsNullOrWhiteSpace(pipRequest.EstateSsn) || !Utils.IsValidSsn(pipRequest.EstateSsn))
        {
            throw new ArgumentException($"Invalid {nameof(pipRequest.EstateSsn)}", nameof(pipRequest));
        }

        if (pipRequest.RecipientSsn is not null && !Utils.IsValidSsn(pipRequest.RecipientSsn))
        {
            throw new ArgumentException($"Invalid {nameof(pipRequest.RecipientSsn)}", nameof(pipRequest));
        }

        // Fetch all roles for the estate and check if there are any assignments with the probate role
        var estateRoleAssignments = await repository.GetRoleAssignmentsForEstate(pipRequest.EstateSsn);
        var isProbateIssued = estateRoleAssignments.Any(ra => ra.RoleCode == Constants.ProbateRoleCode);

        // Filter the role assignments based on the pipRequest
        var roleAssignments = pipRequest.RecipientSsn is not null
            ? estateRoleAssignments.Where(ra => ra.RecipientSsn == pipRequest.RecipientSsn)
            : estateRoleAssignments;

        return new PipResponse
        {
            EstateSsn = pipRequest.EstateSsn,
            RoleAssignments = roleAssignments
                .Select(result => new PipRoleAssignment
                {
                    Id = result.Id,
                    EstateSsn = result.EstateSsn,
                    RoleCode = result.RoleCode,
                    Created = result.Created,
                    HeirSsn = result.HeirSsn,
                    RecipientSsn = result.RecipientSsn,
                    IsRestricted = isProbateIssued && !Constants.ProbateAndProxyRoles.Contains(result.RoleCode)
                })
                .ToList()
        };
    }
}
