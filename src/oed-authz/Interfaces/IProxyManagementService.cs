using oed_authz.Models;

namespace oed_authz.Interfaces;
public interface IProxyManagementService
{
    public Task Add(ProxyManagementRequest proxyManagementRequest);
    public Task Remove(ProxyManagementRequest proxyManagementRequest);
    public Task UpdateProxyRoleAssigments(string estateSsn);
}
