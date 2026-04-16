using System.Collections.Generic;

namespace FEALiTE2D.Api.Contracts.Responses;

public sealed class CaseResultDto
{
    public Dictionary<string, DisplacementDto> NodeDisplacements { get; set; } = new();
    public Dictionary<string, ForceDto> SupportReactions { get; set; } = new();
    public Dictionary<string, List<SegmentDto>> ElementForces { get; set; } = new();
}
