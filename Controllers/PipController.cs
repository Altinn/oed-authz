using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using oed_authz.Interfaces;
using oed_authz.Models;
using oed_authz.Models.Dto;
using oed_authz.Settings;

namespace oed_authz.Controllers;
[ApiController]
public class PipController : Controller
{
    private readonly IPolicyInformationPointService _pipService;

    public PipController(IPolicyInformationPointService pipService)
    {
        _pipService = pipService;
    }

    [HttpPost]
    [Authorize(Policy = Constants.AuthorizationPolicyInternal)]
    [Route("api/v1/pip")]
    public async Task<ActionResult<PipResponseDto>> HandlePipRequest([FromBody] PipRequestDto pipRequestDto)
    {
        try
        {
            return Ok( await HandleRequest(pipRequestDto));
        }
        catch (ArgumentException ex)
        {
            return Problem(
                title: "Bad Input",
                detail: ex.GetType().Name + ": " + ex.Message,
                statusCode: StatusCodes.Status400BadRequest
            );
        }
    }

    private async Task<PipResponseDto> HandleRequest(PipRequestDto pipRequestDto)
    {
        var pipRequest = new PipRequest()
        {
            EstateSsn = pipRequestDto.From,
            RecipientSsn = pipRequestDto.To
        };

        var pipResponse = await _pipService.HandlePipRequest(pipRequest);

        // The roles where there is an heir involved will have three parties (estate, heir and recipient) and
        // is thus not appropiate for this endpoint. This includes the individual proxy role.
        RemoveIndividualProxyRole(pipResponse);

        var pipRoleAssignmentsDto = pipResponse.RoleAssignments.Select(assignment =>
            new PipRoleAssignmentDto
            {
                From = assignment.EstateSsn,
                To = assignment.RecipientSsn,
                Role = assignment.RoleCode,
                Created = assignment.Created
            }).ToList();

        var pipResponseDto = new PipResponseDto()
        {
            RoleAssignments = pipRoleAssignmentsDto
        };

        return pipResponseDto;
    }

    private void RemoveIndividualProxyRole(PipResponse pipResponse)
    {
        pipResponse.RoleAssignments = pipResponse.RoleAssignments.Where(x => !x.RoleCode.Equals(Constants.IndividualProxyRoleCode)).ToList();
    }
}
