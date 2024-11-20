using System.Text.Json.Serialization;

namespace oed_authz.Models.Dto;

public class ProxyAssignmentDto
{
    [JsonRequired]
    public string EstateSsn { get; set; } = null!;

    [JsonRequired]
    public string HeirSsn { get; set; } = null!;

    [JsonRequired]
    public string RecipientSsn { get; set; } = null!;

    [JsonRequired]
    [JsonPropertyName("urn:digitaltdodsbo:rolecode")]
    public string Role { get; set; } = null!;

    public DateTimeOffset? Created { get; set; }
}
