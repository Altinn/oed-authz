using oed_authz.Models;

namespace oed_authz.Interfaces;
public interface IProxyManagementService
{
    public Task Add(ProxyManagementRequest proxyManagementRequest);
    /// <returns>
    /// True when the proxy assignment was removed, false when there was none to remove (for instance
    /// because an earlier request already removed it).
    /// </returns>
    public Task<bool> Remove(ProxyManagementRequest proxyManagementRequest);
    public Task UpdateProxyRoleAssigments(string estateSsn);
}
