namespace oed_authz.Infrastructure.Middleware;

public class LogContextMiddeware(
    RequestDelegate next, 
    ILogger<LogContextMiddeware> logger)
{
    private const string ConsumerClaimType = "consumer";



    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated is not true)
        {
            await next(context);
            return;
        }

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
    public static IApplicationBuilder UseLogContextMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<LogContextMiddeware>();
        return app;
    }
}