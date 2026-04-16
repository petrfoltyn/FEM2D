using System.Text.Json.Serialization;
using FEALiTE2D.Api.Contracts.Enums;

namespace FEALiTE2D.Api.Contracts.Requests;

public sealed class MaterialDto
{
    public string Label { get; set; } = string.Empty;
    public MaterialTypeDto MaterialType { get; set; }

    [JsonPropertyName("E")] public double E { get; set; }
    [JsonPropertyName("U")] public double U { get; set; }

    public double Alpha { get; set; }
    public double Gama { get; set; }
}
