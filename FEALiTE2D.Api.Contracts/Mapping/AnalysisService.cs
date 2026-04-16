using System;
using FEALiTE2D.Api.Contracts.Enums;
using FEALiTE2D.Api.Contracts.Requests;
using FEALiTE2D.Api.Contracts.Responses;
using FEALiTE2D.Api.Contracts.Validation;

namespace FEALiTE2D.Api.Contracts.Mapping;

/// <summary>
/// End-to-end orchestrator: validates an <see cref="AnalysisRequest"/>,
/// builds a <see cref="FEALiTE2D.Structure.Structure"/>, runs <c>Solve()</c>
/// and returns an <see cref="AnalysisResponse"/>.
/// </summary>
public sealed class AnalysisService
{
    public AnalysisResponse Analyze(AnalysisRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var validation = new AnalysisRequestValidator().Validate(request);
        if (!validation.IsValid)
            return AnalysisResponse.Failed(validation.Errors.ToArray());

        var builder = new StructureBuilder();
        FEALiTE2D.Structure.Structure structure;
        try
        {
            structure = builder.Build(request);
        }
        catch (Exception ex)
        {
            return AnalysisResponse.Failed(ex.Message);
        }

        try
        {
            structure.Solve(detailedAnalysisOutput: false);
        }
        catch (Exception ex)
        {
            return AnalysisResponse.Failed(ex.Message);
        }

        var mapper = new ResultMapper();
        var response = new AnalysisResponse { Status = AnalysisStatusDto.Successful };

        foreach (var (label, lc) in builder.LoadCases)
            response.LoadCaseResults[label] = mapper.Map(structure, lc);

        foreach (var (label, combo) in builder.LoadCombinations)
            response.LoadCombinationResults[label] = mapper.Map(structure, combo);

        return response;
    }
}
