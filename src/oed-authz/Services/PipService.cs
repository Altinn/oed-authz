using oed_authz.Interfaces;
using oed_authz.Models;

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

        var roleAssignments = pipRequest.RecipientSsn is not null
            ? await repository.GetRoleAssignmentsForPerson(pipRequest.EstateSsn, pipRequest.RecipientSsn)
            : await repository.GetRoleAssignmentsForEstate(pipRequest.EstateSsn);

        return new PipResponse { 
            EstateSsn = pipRequest.EstateSsn,
            RoleAssignments = roleAssignments
            .Select(result => new PipRoleAssignment
            {
                Id = result.Id,
                EstateSsn = result.EstateSsn,
                RoleCode = result.RoleCode,
                Created = result.Created,
                HeirSsn = result.HeirSsn,
                RecipientSsn = result.RecipientSsn
            })
            .ToList()
        };
    }
}
