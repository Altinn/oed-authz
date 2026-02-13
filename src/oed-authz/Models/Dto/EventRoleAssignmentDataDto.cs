using System.Text.Json.Serialization;

namespace oed_authz.Models.Dto;

//public class EventRoleAssignmentDataDto
//{
//    [JsonPropertyName("caseId")]
//    public string DaCaseId  { get; set; } = string.Empty;

//    [JsonPropertyName("caseStatus")]
//    [JsonConverter(typeof(JsonStringEnumConverter))]
//    public CaseStatus? CaseStatus { get; set; }

//    [JsonPropertyName("heirRoles")]
//    public List<EventRoleAssignmentDto> HeirRoles  { get; set; } = new();
//}


public static class CaseStatus
{
    public static string Mottatt = "MOTTATT";
    public static string Ferdigbehandlet = "FERDIGBEHANDLET";
    public static string Feilfort = "FEILFORT";
    public static string OverfortAnnenDomstol = "OVERFORT_ANNEN_DOMSTOL";
}
