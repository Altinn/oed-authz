namespace oed_authz.Models.Dto;

public class ProxySearchRequestDto
{
    public required string EstateSsn { get; set; }
    public string? RecipientSsn { get; set; }
}
