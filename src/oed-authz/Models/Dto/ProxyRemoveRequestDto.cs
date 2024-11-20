using System.ComponentModel.DataAnnotations;

namespace oed_authz.Models.Dto;

public class ProxyRemoveRequestDto
{
    [Required] public ProxyAssignmentDto Remove { get; set; } = new();

}
