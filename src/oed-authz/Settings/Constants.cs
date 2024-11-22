namespace oed_authz.Settings;

public static class Constants
{
    public const string ConfigurationSectionSecrets = "Secrets";
    public const string ConfigurationSectionGeneralSettings = "GeneralSettings";

    public const string MaskinportenAuthentication = "Maskinporten";
    public const string MaskinportenAuxillaryAuthentication = "MaskinportenAuxillary";

    public const string AuthorizationPolicyInternal = "Internal";
    public const string AuthorizationPolicyForEvents = "Events";
    public const string AuthorizationPolicyExternal = "Externals";

    public const string ScopeInternal = "altinn:dd:internal";
    public const string ScopeExternal = "altinn:dd:authlookup";

    public const string CourtRoleCodePrefix = "urn:domstolene:digitaltdodsbo:";
    public const string HeirRoleCodePrefix = "urn:domstolene:digitaltdodsbo:arving:";
    public const string ProxyRoleCodePrefix = "urn:altinn:digitaltdodsbo:skiftefullmakt:";
    public const string IndividualProxyRoleCode = "urn:altinn:digitaltdodsbo:skiftefullmakt:individuell";
    public const string CollectiveProxyRoleCode = "urn:altinn:digitaltdodsbo:skiftefullmakt:kollektiv";
    public const string ProbateRoleCode = "urn:domstolene:digitaltdodsbo:skifteattest";
    public const string FormuesfullmaktRoleCode = "urn:domstolene:digitaltdodsbo:formuesfullmakt";

    public static IReadOnlyCollection<string> ProbateAndProxyRoles => [ProbateRoleCode, IndividualProxyRoleCode, CollectiveProxyRoleCode];
}

