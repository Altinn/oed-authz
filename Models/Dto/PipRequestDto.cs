namespace oed_authz.Models.Dto;

public class PipRequestDto
{
    public required string From { get; init; }
    public string? To { get; init; }
}
