using oed_authz.Models;

namespace oed_authz.Interfaces;
public interface IAltinnEventHandlerService
{
    public Task HandleEvent(CloudEvent cloudEvent);
}
    