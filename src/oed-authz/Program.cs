using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using oed_authz.Authorization;
using oed_authz.HealthChecks;
using oed_authz.Infrastructure.Database;
using oed_authz.Infrastructure.Middleware;
using oed_authz.Interfaces;
using oed_authz.Repositories;
using oed_authz.Services;
using oed_authz.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<Secrets>(builder.Configuration.GetSection(Constants.ConfigurationSectionSecrets));
builder.Services.Configure<GeneralSettings>(builder.Configuration.GetSection(Constants.ConfigurationSectionGeneralSettings));

builder.Services.AddHealthChecks()
    .AddCheck<InformationCheck>(nameof(InformationCheck))
    .AddOedAuthzDatabaseCheck();

builder.Services.AddScoped<IAltinnEventHandlerService, AltinnEventHandlerService>();
builder.Services.AddScoped<IPolicyInformationPointService, PipService>();
builder.Services.AddScoped<IProxyManagementService, ProxyManagementService>();
builder.Services.AddScoped<IAuthorizationHandler, QueryParamRequirementHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ScopeRequirementHandler>();
builder.Services.AddScoped<IRoleAssignmentsRepository, RoleAssignmentsRepository>();
builder.Services.AddScoped<IEventCursorRepository, EventCursorRepository>();

builder.Services.AddOedAuthzDatabase(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

var appInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
    });
    builder.Services.AddLogging(logging =>
    {
        logging.AddApplicationInsights();
    });
}
else
{
    // Add logging to console if no Application Insights connection string is found
    builder.Services.AddLogging();
}

builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});

builder.Services.AddLogging(logging =>
{
    logging.AddApplicationInsights();
});

builder.Services.AddProblemDetails();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = Constants.MaskinportenOrAltinnAuthentication;
        options.DefaultChallengeScheme = Constants.MaskinportenOrAltinnAuthentication;
    })
    // Add support for the Oauth2 with Maskinporten as issuer
    .AddJwtBearer(Constants.MaskinportenAuthentication, options =>
    {
        options.MetadataAddress =
            builder.Configuration.GetSection(Constants.ConfigurationSectionGeneralSettings)[
                nameof(GeneralSettings.MaskinportenOauth2WellKnownEndpoint)]!;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = false,
            ValidateAudience = false,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    })
    // Add support for Oauth2 with Maskinporten as issuer (auxillary). Used to support "ver" as well as "test"
    // in non-production environments
    .AddJwtBearer(Constants.MaskinportenAuxillaryAuthentication, options =>
    {
        options.MetadataAddress =
            builder.Configuration.GetSection(Constants.ConfigurationSectionGeneralSettings)[
                nameof(GeneralSettings.MaskinportenAuxillaryOauth2WellKnownEndpoint)]!;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = false,
            ValidateAudience = false,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    })
    .AddPolicyScheme(Constants.MaskinportenOrAltinnAuthentication, Constants.MaskinportenOrAltinnAuthentication, options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authHeader = context.Request.Headers[HeaderNames.Authorization].ToString();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                var jwtHandler = new JwtSecurityTokenHandler();

                var mpIssuerSetting = builder.Configuration.GetSection(Constants.ConfigurationSectionGeneralSettings)[
                    nameof(GeneralSettings.MaskinportenOauth2WellKnownEndpoint)]!;
                var mpAuxillaryIssuerSetting = builder.Configuration.GetSection(Constants.ConfigurationSectionGeneralSettings)[
                    nameof(GeneralSettings.MaskinportenAuxillaryOauth2WellKnownEndpoint)]!;

                var issuer = jwtHandler.CanReadToken(token) ? jwtHandler.ReadJwtToken(token).Issuer : null;
                if (issuer is not null)
                {
                    var issuerUri = new Uri(issuer);
                    if (mpAuxillaryIssuerSetting.Contains(issuerUri.Host))
                    {
                        return Constants.MaskinportenAuxillaryAuthentication;
                    }
                    else if (mpIssuerSetting.Contains(issuerUri.Host))
                    {
                        return Constants.MaskinportenAuthentication;
                    }
                }
            }

            return Constants.MaskinportenAuxillaryAuthentication;
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Token/claim-based policy for platform requests to the PIP api
    options.AddPolicy(Constants.AuthorizationPolicyInternal, configurePolicy =>
    {
        configurePolicy
            .RequireAuthenticatedUser()
            .AddRequirements(new ScopeRequirement(Constants.ScopeInternal))
            .Build();
    });

    // Maskinporten scope requirements for external requests.
    options.AddPolicy(Constants.AuthorizationPolicyExternal, configurePolicy =>
    {
        configurePolicy
            .RequireAuthenticatedUser()
            .AddRequirements(new ScopeRequirement(Constants.ScopeExternal))
            .Build();
    });

    // Secret-in-query-param based policy for internal requests to the events endpoint (sent from oed-inbound)
    options.AddPolicy(Constants.AuthorizationPolicyForEvents, configurePolicy =>
    {
        configurePolicy.Requirements.Add(new QueryParamRequirement(
            builder.Configuration.GetSection(Constants.ConfigurationSectionGeneralSettings)[
                nameof(GeneralSettings.OedEventAuthQueryParameter)]!,
            builder.Configuration.GetSection(Constants.ConfigurationSectionSecrets)[
                nameof(Secrets.OedEventAuthKey)]!));
        configurePolicy.Build();
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAzurePortal", builder =>
        builder
            .WithOrigins("https://portal.azure.com")
            .AllowAnyMethod()
            .AllowAnyHeader());
});

if (builder.Environment.IsDevelopment())
{
    IdentityModelEventSource.ShowPII = true;
    builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly());
}

var app = builder.Build();

app.UseCors("AllowAzurePortal");

//TODO: Fjern denne når platform har endret TF

if (!app.Environment.IsProduction())
{
    app.UseLogContextMiddleware();
}

// Liveness probe
app.MapHealthChecks("/", new HealthCheckOptions
{
    Predicate = _ => false, // No custom "deep" checks here
});

// Helsesjekk på /health
// Liveness
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false, // No custom "deep" checks here
});

app.MapHealthChecks("/health/auth", new HealthCheckOptions
{
    Predicate = _ => false, // No custom "deep" checks here
}).RequireAuthorization(Constants.AuthorizationPolicyExternal);

// Details
app.MapHealthChecks("/health/details", new HealthCheckOptions
{
    ResponseWriter = JsonHealthResponseWriter.WriteResponseAsync
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseExceptionHandler(app.Environment.IsDevelopment() ? "/error-development" : "/error");

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Running database migrations on startup
await app.MigrateOedAuthzDatabase();

app.Run();
