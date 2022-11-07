﻿using oed_authz.Interfaces;
using oed_authz.Models;

namespace oed_authz.Services;
public class PolicyInformationPointService : IPolicyInformationPointService
{
    private readonly IOedRoleRepositoryService _oedRoleRepositoryService;

    public PolicyInformationPointService(IOedRoleRepositoryService oedRoleRepositoryService)
    {
        _oedRoleRepositoryService = oedRoleRepositoryService;
    }

    public async Task<PipResponse> HandlePipRequest(PipRequest pipRequest)
    {
        var results = new List<OedRoleAssignment>();
        if (pipRequest.CoveredBy != null && pipRequest.OfferedBy != null)
        {
            results = await _oedRoleRepositoryService.GetRoleAssignmentsForUser(pipRequest.CoveredBy, pipRequest.OfferedBy);
        }
        else if (pipRequest.CoveredBy != null)
        {
            results = await _oedRoleRepositoryService.GetRoleAssignmentsForUser(pipRequest.CoveredBy);
        }
        else if (pipRequest.OfferedBy != null)
        {
            results = await _oedRoleRepositoryService.GetRoleAssignmentsForEstate(pipRequest.OfferedBy);
        }

        var pipResponse = new PipResponse();
        foreach (var result in results)
        {
            pipResponse.RoleAssignments.Add(new PipRoleAssignment
            {
                OfferedBy = result.EstateSsn,
                CoveredBy = result.RecipientSsn,
                RoleCode = result.RoleCode
            });
        }

        return pipResponse;
    }
}