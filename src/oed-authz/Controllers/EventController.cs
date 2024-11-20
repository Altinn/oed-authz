using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using oed_authz.Interfaces;
using oed_authz.Models;
using oed_authz.Settings;

namespace oed_authz.Controllers;

[Route("api/v1/eventhandler")]
[ApiController]
public class EventController : Controller
{
    private readonly IAltinnEventHandlerService _altinnEventHandlerService;

    public EventController(IAltinnEventHandlerService altinnEventHandlerService)
    {
        _altinnEventHandlerService = altinnEventHandlerService;
    }

    /// <summary>
    /// This is the endpoint that Altinn will call when it wants to send us an event.
    /// </summary>
    /// <param name="cloudEvent"></param>
    /// <returns></returns>
    [HttpPost]
    [Authorize(Policy = Constants.AuthorizationPolicyForEvents)]
    public async Task<IActionResult> HandleCloudEvent([FromBody] CloudEvent cloudEvent)
    {
        try
        {
            await _altinnEventHandlerService.HandleEvent(cloudEvent);
        }
        catch (ArgumentException ex)
        {
            return Problem(
                title: "Bad Input",
                detail: ex.GetType().Name + ": " + ex.Message,
                statusCode: StatusCodes.Status400BadRequest
            );
        }
        catch (PostgresException ex)
        {
            if (ex.Message.Contains("duplicate"))
            {
                // Handle duplicate role assignments gracefully 
                return new OkResult();
            }

            throw;
        }
        
        return new OkResult();
    }
}
