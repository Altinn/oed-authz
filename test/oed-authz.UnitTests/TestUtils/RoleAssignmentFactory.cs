using FakeItEasy;
using oed_authz.Interfaces;
using oed_authz.Models;
using oed_authz.Settings;

namespace oed_authz.UnitTests.TestUtils;

internal class RoleAssignmentFactory(string estateSsn)
{
    private long _idCounter;
    
    private RoleAssignment Create(string recipientSsn, string roleCode, string? heirSsn = null)
    {
        return new RoleAssignment
        {
            EstateSsn = estateSsn,
            RecipientSsn = recipientSsn,
            HeirSsn = heirSsn,
            RoleCode = roleCode,
            Id = ++_idCounter,
            Created = DateTimeOffset.UtcNow
        };
    }

    public RoleAssignment ProbateRole(string recipientSsn) =>
        Create(recipientSsn, Constants.ProbateRoleCode);

    public RoleAssignment FormuesfulmaktRole(string recipientSsn) =>
        Create(recipientSsn, Constants.FormuesfullmaktRoleCode);

    public RoleAssignment CollectiveProxyRole(string recipientSsn) =>
        Create(recipientSsn, Constants.CollectiveProxyRoleCode);

    public RoleAssignment IndividualProxyRole(string heirSsn, string proxySsn) =>
        Create(proxySsn, Constants.IndividualProxyRoleCode, heirSsn);

}