using System.Collections.ObjectModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace oed_authz.HealthChecks
{
    public class InformationCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            var data = new Dictionary<string, object>
            {
                { "os", Environment.OSVersion },
                { ".net version", Environment.Version.ToString() }
            };

            return Task.FromResult(HealthCheckResult.Healthy("Information from the system", data));
        }
    }
}
