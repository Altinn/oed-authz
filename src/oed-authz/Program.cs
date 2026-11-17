using System.Reflection;
using Microsoft.AspNetCore.Authentication;
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
using oed_authz.Utils;

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

var generalSettings = builder.Configuration.GetSection(Constants.ConfigurationSectionGeneralSettings);
var maskinportenIssuerHost =
    new Uri(generalSettings[nameof(GeneralSettings.MaskinportenOauth2WellKnownEndpoint)]!).Host;
var maskinportenAuxillaryIssuerHost =
    new Uri(generalSettings[nameof(GeneralSettings.MaskinportenAuxillaryOauth2WellKnownEndpoint)]!).Host;

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
            // No ValidIssuer: the handler validates against the issuer in the well-known metadata
            ValidateIssuer = true,
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
            // No ValidIssuer: the handler validates against the issuer in the well-known metadata
            ValidateIssuer = true,
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
            string? issuer = null;

            // Tokens from unknown issuers are rejected instead of falling back to one of the JwtBearer schemes
            var scheme = Constants.RejectedIssuerAuthentication;

            var authHeader = context.Request.Headers[HeaderNames.Authorization].ToString();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader["Bearer ".Length..].Trim();
                issuer = JwtReader.GetIssuerFromJwt(token);

                if (Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri))
                {
                    if (string.Equals(issuerUri.Host, maskinportenAuxillaryIssuerHost, StringComparison.OrdinalIgnoreCase))
                    {
                        scheme = Constants.MaskinportenAuxillaryAuthentication;
                    }
                    else if (string.Equals(issuerUri.Host, maskinportenIssuerHost, StringComparison.OrdinalIgnoreCase))
                    {
                        scheme = Constants.MaskinportenAuthentication;
                    }
                }
            }

            // Records which issuers real callers use, so issuer enforcement can be verified per environment
            if (!string.IsNullOrEmpty(authHeader))
            {
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("oed_authz.Authentication.IssuerSelector");
                logger.Log(
                    scheme == Constants.RejectedIssuerAuthentication ? LogLevel.Warning : LogLevel.Information,
                    "Bearer token with issuer {TokenIssuer} on {Path} forwarded to authentication scheme {AuthenticationScheme}",
                    issuer ?? "(unreadable)", context.Request.Path.Value, scheme);
            }

            return scheme;
        };
    })
    .AddScheme<AuthenticationSchemeOptions, RejectedIssuerAuthenticationHandler>(
        Constants.RejectedIssuerAuthentication, _ => { });

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

app.UseConsumerLogging();

app.MapControllers();

// Running database migrations on startup
await app.MigrateOedAuthzDatabase();

app.Run();
