using System.Text.Json.Serialization;

namespace oed_authz.Models.Dto;

public class ProxySearchAssignmentDto
{
    public string EstateSsn { get; set; } = null!;

    public string? HeirSsn { get; set; }

    public string RecipientSsn { get; set; } = null!;

    [JsonPropertyName("urn:digitaltdodsbo:rolecode")]
    public string Role { get; set; } = null!;

    public DateTimeOffset? Created { get; set; }
}
