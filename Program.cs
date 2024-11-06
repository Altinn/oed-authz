using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using oed_authz.Authorization;
using oed_authz.Helpers;
using oed_authz.Interfaces;
using oed_authz.Services;
using oed_authz.Settings;
using Yuniql.AspNetCore;
using Yuniql.PostgreSql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<Secrets>(builder.Configuration.GetSection(Constants.ConfigurationSectionSecrets));
builder.Services.Configure<GeneralSettings>(builder.Configuration.GetSection(Constants.ConfigurationSectionGeneralSettings));

builder.Services.AddSingleton<IAltinnEventHandlerService, AltinnEventHandlerService>();
builder.Services.AddSingleton<IOedRoleRepositoryService, OedRoleRepositoryService>();
builder.Services.AddSingleton<IPolicyInformationPointService, PipService>();
builder.Services.AddSingleton<IProxyManagementService, ProxyManagementService>();
builder.Services.AddScoped<IAuthorizationHandler, QueryParamRequirementHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ScopeRequirementHandler>();

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
        logging.AddFilter<ApplicationInsightsLoggerProvider>("", LogLevel.Information); // Set the minimum log level to Information
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
    logging.AddFilter<ApplicationInsightsLoggerProvider>("", LogLevel.Information); // Set the minimum log level to Information
});

builder.Services.AddProblemDetails();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = Constants.MaskinportenAuthentication;
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
    });

builder.Services.AddAuthorization(options =>
{
    // Token/claim-based policy for platform requests to the PIP api
    options.AddPolicy(Constants.AuthorizationPolicyInternal, configurePolicy =>
    {
        configurePolicy
            .RequireAuthenticatedUser()
            .AddAuthenticationSchemes(Constants.MaskinportenAuthentication, Constants.MaskinportenAuxillaryAuthentication)
            .AddRequirements(new ScopeRequirement(Constants.ScopeInternal))
            .Build();
    });

    // Maskinporten scope requirements for external requests.
    options.AddPolicy(Constants.AuthorizationPolicyExternal, configurePolicy =>
    {
        configurePolicy
            .RequireAuthenticatedUser()
            .AddAuthenticationSchemes(Constants.MaskinportenAuthentication, Constants.MaskinportenAuxillaryAuthentication)
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
    options.AddPolicy("AllowAzurePortal",
        builder => builder.WithOrigins("https://portal.azure.com")
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

app.UseSwagger();
app.UseSwaggerUI();

app.UseExceptionHandler(app.Environment.IsDevelopment() ? "/error-development" : "/error");

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();


var traceService = new ConsoleTraceService { IsDebugEnabled = true };
app.UseYuniql(
    new PostgreSqlDataService(traceService),
    new PostgreSqlBulkImportService(traceService),
    traceService,
    new Configuration
    {
        Workspace = Path.Combine(Environment.CurrentDirectory, "Migrations"),
        ConnectionString = builder.Configuration.GetSection(Constants.ConfigurationSectionSecrets)[
            nameof(Secrets.PostgreSqlAdminConnectionString)]!,
        IsAutoCreateDatabase = false,
        IsDebug = true
    });

app.Run();
