using System.Text.Json.Serialization;

namespace oed_authz.Models.Dto;
public class EventRoleAssignmentDto
{
    [JsonPropertyName("nin")]
    public string Nin { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
}
