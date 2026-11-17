using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace oed_authz.Authorization;

/// <summary>
/// Authentication scheme for requests whose bearer token is missing, unreadable or from an issuer
/// this service does not trust. Requests without an Authorization header get no result, so anonymous
/// endpoints are unaffected. Any other request fails authentication.
/// </summary>
public class RejectedIssuerAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(Request.Headers.Authorization.Count == 0
            ? AuthenticateResult.NoResult()
            : AuthenticateResult.Fail("The bearer token is not from an accepted issuer."));
}
