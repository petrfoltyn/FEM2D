using System.Collections.Generic;
using FEALiTE2D.Api.Contracts.Enums;

namespace FEALiTE2D.Api.Contracts.Requests;

public sealed class LoadCaseDto
{
    public string Label { get; set; } = string.Empty;
    public LoadCaseTypeDto LoadCaseType { get; set; }
    public LoadCaseDurationDto LoadCaseDuration { get; set; } = LoadCaseDurationDto.Permanent;

    public List<NodalLoadDto> NodalLoads { get; set; } = new();
    public List<ElementLoadDto> ElementLoads { get; set; } = new();
    public List<SupportDisplacementDto> SupportDisplacements { get; set; } = new();
}
