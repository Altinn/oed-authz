using System.ComponentModel.DataAnnotations;

namespace oed_authz.Models.Dto;

public class ProxyAddRequestDto
{
    [Required] public ProxyAssignmentDto Add { get; set; } = new();
}
