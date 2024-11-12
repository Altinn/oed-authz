using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace oed_authz.HealthChecks
{
    public class JsonHealthResponseWriter
    {
        public static async Task WriteResponseAsync(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json";

            var response = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description ?? "No description",
                    duration = entry.Value.Duration.TotalMilliseconds,
                    data = entry.Value.Data
                }),
                totalDuration = report.TotalDuration.TotalMilliseconds
            };

            await context.Response.WriteAsJsonAsync(response, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
    }
}
