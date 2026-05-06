# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**oed-authz** is an ASP.NET Core 10 Web API that manages authorization for OED (Digital Estate / Digitalt Dødsbo) in Norway. It serves as a Policy Information Point (PIP) for Altinn Authorization and manages role assignments and proxy delegations among heirs in estate cases.

## Build and Test Commands

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run all tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run application locally
dotnet run --project src/oed-authz/oed-authz.csproj

# Add EF Core migration
dotnet ef migrations add MigrationName --project src/oed-authz

# Update database
dotnet ef database update --project src/oed-authz
```

CI runs on ubuntu-latest with .NET 10.x. The CD pipeline deploys to Azure App Service on merge to master.

## Architecture

### Solution Structure

```
src/oed-authz/       Main ASP.NET Core 10 Web API
test/oed-authz.UnitTests/        xUnit + FakeItEasy + FluentAssertions
test/oed-authz.IntegrationTests/ xUnit + Testcontainers.PostgreSQL (requires Docker)
```

### API Surface

| Endpoint | Auth Policy | Purpose |
|----------|-------------|---------|
| `POST /api/v1/authorization/roles/search` | External (`altinn:dd:authlookup`) | Estate role lookup for external consumers |
| `POST /api/v1/authorization/proxies/search` | External | Proxy assignment lookup |
| `POST /api/v1/authorization/proxies/add` | Internal (`altinn:dd:internal`) | Add proxy role |
| `POST /api/v1/authorization/proxies/remove` | Internal | Remove proxy role |
| `POST /api/v1/pip` | Internal | Policy Information Point for Altinn |
| `POST /api/v1/eventhandler` | Query param secret | CloudEvent receiver from Altinn |
| `GET /health`, `/health/details`, `/health/auth` | — | Health probes |

### Key Services and Patterns

**AltinnEventHandlerService** — processes CloudEvents from Altinn, updates court-assigned role assignments. Uses `EventCursor` to detect and discard out-of-order events per estate/event-type pair.

**PipService** — resolves roles for Altinn Authorization PIP lookups; filters based on caller scope.

**ProxyManagementService** — manages individual proxy assignments and auto-assigns/revokes collective proxy roles when all heirs delegate to the same recipient.

**RoleAssignmentsRepository / EventCursorRepository** — EF Core data access. The app uses two DB users: `oedpgadmin` for migrations (startup) and `oedpguser` for runtime queries.

### Role Types

- **Court-assigned**: `urn:domstolene:digitaltdodsbo:formuesfullmakt`, `urn:domstolene:digitaltdodsbo:skifteattest`
- **Individual proxy**: `urn:altinn:digitaltdodsbo:skiftefullmakt:individuell`
- **Collective proxy**: `urn:altinn:digitaltdodsbo:skiftefullmakt:kollektiv` (auto-managed)
- **Superadmin role**: filtered from all external-facing responses

### Authentication

Two Maskinporten JWT handlers registered under a policy scheme that routes by issuer:
- Primary: `test.maskinporten.no` (production uses the production endpoint)
- Auxiliary: `platform.tt02.altinn.no` (non-prod only)

Event handler uses a query parameter secret (`OedEventAuthKey`) instead of JWT.

### Database

PostgreSQL 13+, schema `oedauthz`. Tables: `roleassignments`, `eventcursor`. Migrations run automatically on startup using the admin connection string.

Local development connection strings (from `appsettings.Development.json`):
- User: `Server=localhost;Username=oedpguser;Database=oedauthz;Port=5432;Password=secret;SSLMode=Prefer`
- Admin: `Server=localhost;Username=oedpgadmin;Database=oedauthz;Port=5432;Password=secret;SSLMode=Prefer`

### Configuration

`appsettings.json` holds non-secret settings (`GeneralSettings`). Secrets (connection strings, event auth key) are injected via Azure Key Vault in production and via `dotnet user-secrets` in development.

```json
{
  "GeneralSettings": {
    "MaskinportenOauth2WellKnownEndpoint": "...",
    "MaskinportenAuxillaryOauth2WellKnownEndpoint": "...",
    "OedEventAuthQueryParameter": "auth"
  },
  "Secrets": {
    "PostgreSqlUserConnectionString": "",
    "PostgreSqlAdminConnectionString": "",
    "OedEventAuthKey": ""
  }
}
```

Optional: set `APPLICATIONINSIGHTS_CONNECTION_STRING` env var to enable telemetry.

## Important Constraints

- Integration tests require Docker (Testcontainers spins up a real PostgreSQL container).
- Roles must not be cached by external consumers — they can change at any time.
- Only Norwegian 11-digit SSNs are supported as identifiers.
- Out-of-order CloudEvents are silently discarded based on the `EventCursor` timestamp tracking.
