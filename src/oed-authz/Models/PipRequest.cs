namespace oed_authz.Models;

public class PipRequest
{
    public required string EstateSsn { get; init; }
    public string? RecipientSsn { get; init; }
}
