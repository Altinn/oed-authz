using oed_authz.Models;

namespace oed_authz.Interfaces;
public interface IPolicyInformationPointService
{
    public Task<PipResponse> HandlePipRequest(PipRequest pipRequest);
}
