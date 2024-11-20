using System.Text.Json.Serialization;

namespace oed_authz.Models.Dto;

public class PipRoleAssignmentDto
{
    public string From { get; init; } = null!;

    public string To { get; init; } = null!;

    [JsonPropertyName("urn:digitaltdodsbo:rolecode")]
    public string Role { get; init; } = null!;

    public DateTimeOffset Created { get; init; }
}
