namespace oed_authz.Settings;

public class Secrets
{
    public string PostgreSqlUserConnectionString { get; set; } = null!;
    public string PostgreSqlAdminConnectionString { get; set; } = null!;
    public string OedEventAuthKey { get; set; } = null!;
}
