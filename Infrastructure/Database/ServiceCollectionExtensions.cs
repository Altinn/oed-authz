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

        services.AddDbContextPool<OedAuthzDbContext>(opt =>
                opt.UseNpgsql(
                    configuration.GetSection(
                        Constants.ConfigurationSectionSecrets)[nameof(Secrets.PostgreSqlAdminConnectionString)]!));

        return services;
    }
}

