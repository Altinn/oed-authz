namespace oed_authz.Infrastructure.Middleware;

public class ConsumerLoggingMiddleware(
    RequestDelegate next, 
    ILogger<ConsumerLoggingMiddleware> logger)
{
    private const string ConsumerClaimType = "consumer";

    public async Task InvokeAsync(HttpContext context)
    {
        var consumer = context.User.Claims.FirstOrDefault(c => c.Type == ConsumerClaimType)?.Value;
        var state = new Dictionary<string, object>
        {
            { "Consumer", consumer ?? "Unknown" }
        };

        using (logger.BeginScope(state))
        {
            await next(context);
        }
    }
}

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseConsumerLogging(this IApplicationBuilder app)
    {
        app.UseMiddleware<ConsumerLoggingMiddleware>();
        return app;
    }
}