using System.Collections.Generic;

namespace FEALiTE2D.Api.Contracts.Responses;

public sealed class CaseResultDto
{
    public Dictionary<string, DisplacementDto> NodeDisplacements { get; set; } = new();
    public Dictionary<string, ForceDto> SupportReactions { get; set; } = new();
    public Dictionary<string, List<SegmentDto>> ElementForces { get; set; } = new();

    /// <summary>
    /// Forces of the parts of a composite member (time-dependent solver only, request elements with parallel
    /// <c>TimeDependent.AdditionalElements</c>): key = request element, value = part label (the element itself or an
    /// additional element) → segments in the axis of that part, on the same mesh as <see cref="ElementForces"/>.
    /// <see cref="ElementForces"/> of such an element is the resultant of all parts related to the node axis.
    /// Null when there are no parts (always for FEALiTE2D).
    /// </summary>
    public Dictionary<string, Dictionary<string, List<SegmentDto>>>? PartForces { get; set; }
}
