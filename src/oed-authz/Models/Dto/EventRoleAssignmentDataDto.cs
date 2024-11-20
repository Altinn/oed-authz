using System.Text.Json.Serialization;

namespace oed_authz.Models.Dto;

public class EventRoleAssignmentDataDto
{
    [JsonPropertyName("caseId")]
    public string DaCaseId  { get; set; } = string.Empty;

    [JsonPropertyName("caseStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CaseStatus? CaseStatus { get; set; }

    [JsonPropertyName("heirRoles")]
    public List<EventRoleAssignmentDto> HeirRoles  { get; set; } = new();
}

public enum CaseStatus
{
    [System.Runtime.Serialization.EnumMember(Value = @"MOTTATT")]
    MOTTATT = 0,

    [System.Runtime.Serialization.EnumMember(Value = @"FERDIGBEHANDLET")]
    FERDIGBEHANDLET = 1,

    [System.Runtime.Serialization.EnumMember(Value = @"FEILFORT")]
    FEILFORT = 2,
}

