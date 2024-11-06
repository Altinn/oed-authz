using oed_authz.Interfaces;
using oed_authz.Models;

namespace oed_authz.Services;
public class PipService : IPolicyInformationPointService
{
    private readonly IOedRoleRepositoryService _oedRoleRepositoryService;

    public PipService(IOedRoleRepositoryService oedRoleRepositoryService)
    {
        _oedRoleRepositoryService = oedRoleRepositoryService;
    }

    public async Task<PipResponse> HandlePipRequest(PipRequest pipRequest)
    {
        if (pipRequest.RecipientSsn is not null && !Utils.IsValidSsn(pipRequest.RecipientSsn))
        {
            throw new ArgumentException(nameof(pipRequest.RecipientSsn));
        }

        if (pipRequest.EstateSsn is not null && !Utils.IsValidSsn(pipRequest.EstateSsn))
        {
            throw new ArgumentException(nameof(pipRequest.EstateSsn));
        }

        List<RepositoryRoleAssignment> roleAssignments;
        if (pipRequest.RecipientSsn is not null && pipRequest.EstateSsn is not null)
        {
            roleAssignments = await _oedRoleRepositoryService.GetRoleAssignmentsForPerson(pipRequest.RecipientSsn, pipRequest.EstateSsn);
        }
        else if (pipRequest.RecipientSsn is not null)
        {
            roleAssignments = await _oedRoleRepositoryService.GetRoleAssignmentsForPerson(pipRequest.RecipientSsn);
        }
        else if (pipRequest.EstateSsn is not null)
        {
            roleAssignments = await _oedRoleRepositoryService.GetRoleAssignmentsForEstate(pipRequest.EstateSsn);
        }
        else
        {
            throw new ArgumentNullException(nameof(pipRequest), "Both recipientSsn and estateSsn cannot be null");
        }

        var pipRoleAssignments = new List<PipRoleAssignment>();
        foreach (var result in roleAssignments)
        {
            pipRoleAssignments.Add(new PipRoleAssignment
            {
                Id = result.Id,
                EstateSsn = result.EstateSsn,
                RoleCode = result.RoleCode,
                Created = result.Created,
                HeirSsn = result.HeirSsn,
                RecipientSsn = result.RecipientSsn
            });
        }

        return new PipResponse { RoleAssignments = pipRoleAssignments };
    }
}
