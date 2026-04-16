using System.Collections.Generic;

namespace FEALiTE2D.Api.Contracts.Requests;

public sealed class AnalysisRequest
{
    public List<NodeDto> Nodes { get; set; } = new();
    public List<MaterialDto> Materials { get; set; } = new();
    public List<SectionDto> Sections { get; set; } = new();
    public List<ElementDto> Elements { get; set; } = new();
    public List<LoadCaseDto> LoadCases { get; set; } = new();
    public List<LoadCombinationDto> LoadCombinations { get; set; } = new();
    public SettingsDto? Settings { get; set; }
}
