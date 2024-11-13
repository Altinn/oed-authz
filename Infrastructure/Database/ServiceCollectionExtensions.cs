using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using oed_authz.Settings;

namespace oed_authz.Infrastructure.Database;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOedAuthzDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContextPool<OedAuthzDbContext>((IServiceProvider provider, DbContextOptionsBuilder optionsBuilder) =>
        {
            var secrets = provider.GetRequiredService<IOptions<Secrets>>();
            optionsBuilder.UseNpgsql(secrets.Value.PostgreSqlUserConnectionString);
        });

        return services;
    }
}

public static class WebAppExtensions
{
    /// <summary>
    /// Extension method for running database migrations using the admin connectionstring
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static async Task MigrateOedAuthzDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var secrets = scope.ServiceProvider.GetRequiredService<IOptions<Secrets>>();

        var optionsBuilder = new DbContextOptionsBuilder<OedAuthzDbContext>()
            .UseNpgsql(secrets.Value.PostgreSqlAdminConnectionString)
            .UseLoggerFactory(loggerFactory);

        using var dbContext = new OedAuthzDbContext(optionsBuilder.Options);
        await dbContext.Database.MigrateAsync();
    }
}

