namespace oed_authz.Models.Dto;

public class RolesSearchRequestDto
{
    public required string EstateSsn { get; set; }
    public string? RecipientSsn { get; set; }
}
