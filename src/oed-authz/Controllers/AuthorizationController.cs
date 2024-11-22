using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using oed_authz.Interfaces;
using oed_authz.Models;
using oed_authz.Models.Dto;
using oed_authz.Settings;

namespace oed_authz.Controllers;

[ApiController]
[Route("api/v1/authorization")]
public class AuthorizationController : Controller
{
    private readonly IPolicyInformationPointService _pipService;
    private readonly IProxyManagementService _papService;

    public AuthorizationController(IPolicyInformationPointService pipService, IProxyManagementService papService)
    {
        _pipService = pipService;
        _papService = papService;
    }

    [HttpPost]
    [Route("roles/search")]
    [Authorize(Policy = Constants.AuthorizationPolicyExternal)]
    public async Task<ActionResult<RolesSearchResponseDto>> GetRoles([FromBody] RolesSearchRequestDto rolesSearchRequestDto)
    {
        try
        {
            return Ok(await HandleRequest(rolesSearchRequestDto));
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

    [HttpPost]
    [Route("proxies/search")]
    [Authorize(Policy = Constants.AuthorizationPolicyExternal)]
    public async Task<ActionResult<ProxySearchResponseDto>> GetProxies([FromBody] ProxySearchRequestDto proxySearchRequestDto)
    {
        try
        {
            return Ok(await HandleRequest(proxySearchRequestDto));
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

    [HttpPost]
    [Route("proxies/add")]
    [Authorize(Policy = Constants.AuthorizationPolicyInternal)]
    public async Task<ActionResult> AddProxy([FromBody] ProxyAddRequestDto proxyAddRequestDto)
    {
        try
        {
            return new ObjectResult(await HandleRequest(proxyAddRequestDto)) { StatusCode = StatusCodes.Status201Created };
        }
        catch (ArgumentException ex)
        {
            return Problem(
                title: "Bad Input",
                detail: ex.GetType().Name + ": " + ex.Message,
                statusCode: StatusCodes.Status400BadRequest
            );
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return Problem(
                title: "Constraint Violation",
                detail: "There can only be one proxy assignment per estate-heir-recipient-role combination.",
                statusCode: StatusCodes.Status409Conflict
            );
        }
    }

    [HttpPost]
    [Route("proxies/remove")]
    [Authorize(Policy = Constants.AuthorizationPolicyInternal)]
    public async Task<ActionResult> RemoveProxy([FromBody] ProxyRemoveRequestDto proxyRemoveRequestDto)
    {
        try
        {
            await HandleRequest(proxyRemoveRequestDto);
            return NoContent();
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

    private async Task<RolesSearchResponseDto> HandleRequest(RolesSearchRequestDto rolesSearchRequestDto)
    {
        var pipRequest = new PipRequest
        {
            RecipientSsn = rolesSearchRequestDto.RecipientSsn,
            EstateSsn = rolesSearchRequestDto.EstateSsn
        };

        var pipResponse = await _pipService.HandlePipRequest(pipRequest);

        var roleAssignmentDtos =
            pipResponse.RoleAssignments.Select(pipRoleAssignment =>
                new RoleAssignmentDto()
                {
                    EstateSsn = pipRoleAssignment.EstateSsn,
                    RecipientSsn = pipRoleAssignment.RecipientSsn,
                    Role = pipRoleAssignment.RoleCode,
                    Created = pipRoleAssignment.Created,
                    IsRestricted = pipRoleAssignment.IsRestricted
                }).ToList();

        var rolesSearchResponseDto = new RolesSearchResponseDto()
        {
            RoleAssignments = roleAssignmentDtos
        };

        return rolesSearchResponseDto;
    }

    private async Task<ProxySearchResponseDto> HandleRequest(ProxySearchRequestDto proxySearchRequestDto)
    {
        var pipRequest = new PipRequest
        {
            RecipientSsn = proxySearchRequestDto.RecipientSsn,
            EstateSsn = proxySearchRequestDto.EstateSsn
        };

        var pipResponse = await _pipService.HandlePipRequest(pipRequest);

        // We only want to return the proxy roles (not including court roles or heir relation roles)
        FilterProxyRoles(pipResponse);

        var proxyAssignmentDtos =
            pipResponse.RoleAssignments.Select(pipRoleAssignment =>
                new ProxySearchAssignmentDto()
                {
                    EstateSsn = pipRoleAssignment.EstateSsn,
                    HeirSsn = pipRoleAssignment.HeirSsn,
                    RecipientSsn = pipRoleAssignment.RecipientSsn,
                    Role = pipRoleAssignment.RoleCode,
                    Created = pipRoleAssignment.Created
                }).ToList();

        var proxySearchResponseDto = new ProxySearchResponseDto()
        {
            ProxyAssignments = proxyAssignmentDtos
        };

        return proxySearchResponseDto;
    }

    private async Task<ProxySearchResponseDto> HandleRequest(ProxyAddRequestDto proxyAddRequestDto)
    {
        var papRequest = new ProxyManagementRequest
        {
            EstateSsn = proxyAddRequestDto.Add.EstateSsn,
            ProxyRoleAssignment = new ProxyRoleAssignment
            {
                HeirSsn = proxyAddRequestDto.Add.HeirSsn,
                RecipientSsn = proxyAddRequestDto.Add.RecipientSsn,
                RoleCode = proxyAddRequestDto.Add.Role,
                Created = DateTime.UtcNow
            }
        };

        await _papService.Add(papRequest);

        return await HandleRequest(new ProxySearchRequestDto
        {
            EstateSsn = proxyAddRequestDto.Add.EstateSsn
        });
    }

    private async Task HandleRequest(ProxyRemoveRequestDto proxyRemoveRequestDto)
    {
        var papRequest = new ProxyManagementRequest
        {
            EstateSsn = proxyRemoveRequestDto.Remove.EstateSsn,
            ProxyRoleAssignment = new ProxyRoleAssignment
            {
                HeirSsn = proxyRemoveRequestDto.Remove.HeirSsn,
                RecipientSsn = proxyRemoveRequestDto.Remove.RecipientSsn,
                RoleCode = proxyRemoveRequestDto.Remove.Role
            }
        };

        await _papService.Remove(papRequest);
    }

    private void FilterProxyRoles(PipResponse pipResponse)
    {
        pipResponse.RoleAssignments = pipResponse.RoleAssignments.Where(x => x.RoleCode.StartsWith(Constants.ProxyRoleCodePrefix)).ToList();
    }
}
