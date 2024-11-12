using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace oed_authz.HealthChecks
{
    public class HealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                HealthCheckResult.Healthy("Health check reponded properly."));
        }
    }
}
