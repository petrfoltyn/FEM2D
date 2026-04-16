using System.Collections.Generic;

namespace FEALiTE2D.Api.Contracts.Requests;

public sealed class LoadCombinationDto
{
    public string Label { get; set; } = string.Empty;
    public Dictionary<string, double> Factors { get; set; } = new();
}
