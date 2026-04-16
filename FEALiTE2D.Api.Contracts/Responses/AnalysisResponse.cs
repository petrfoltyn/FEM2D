using System.Collections.Generic;
using FEALiTE2D.Api.Contracts.Enums;

namespace FEALiTE2D.Api.Contracts.Responses;

public sealed class AnalysisResponse
{
    public AnalysisStatusDto Status { get; set; } = AnalysisStatusDto.Successful;
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, CaseResultDto> LoadCaseResults { get; set; } = new();
    public Dictionary<string, CaseResultDto> LoadCombinationResults { get; set; } = new();

    public static AnalysisResponse Failed(params string[] errors) => new()
    {
        Status = AnalysisStatusDto.Failure,
        Errors = new List<string>(errors)
    };
}
