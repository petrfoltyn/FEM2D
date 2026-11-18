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

    /// <summary>
    /// Optional time-dependent data (stages, creep, shrinkage) for a time-dependent solver over the same structure.
    /// FEALiTE2D itself ignores it and computes the instantaneous response at 28 days.
    /// </summary>
    public TimeDependentDto? TimeDependent { get; set; }
}
