using System.Text.Json.Serialization;

namespace FEALiTE2D.Api.Contracts.Requests;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(GenericSectionDto), typeDiscriminator: "Generic")]
[JsonDerivedType(typeof(RectangularSectionDto), typeDiscriminator: "Rectangular")]
[JsonDerivedType(typeof(CircularSectionDto), typeDiscriminator: "Circular")]
[JsonDerivedType(typeof(IPESectionDto), typeDiscriminator: "IPE")]
[JsonDerivedType(typeof(HollowTubeSectionDto), typeDiscriminator: "HollowTube")]
public abstract class SectionDto
{
    public string Label { get; set; } = string.Empty;
    public string MaterialLabel { get; set; } = string.Empty;
}

public sealed class GenericSectionDto : SectionDto
{
    [JsonPropertyName("A")] public double A { get; set; }
    [JsonPropertyName("Az")] public double Az { get; set; }
    [JsonPropertyName("Ay")] public double Ay { get; set; }
    [JsonPropertyName("Iz")] public double Iz { get; set; }
    [JsonPropertyName("Iy")] public double Iy { get; set; }
    [JsonPropertyName("J")] public double J { get; set; }
    public double MaxHeight { get; set; }
    public double MaxWidth { get; set; }
}

public sealed class RectangularSectionDto : SectionDto
{
    public double B { get; set; }
    public double T { get; set; }
}

public sealed class CircularSectionDto : SectionDto
{
    public double D { get; set; }
}

public sealed class IPESectionDto : SectionDto
{
    public double Tf { get; set; }
    public double Tw { get; set; }
    public double B { get; set; }
    public double H { get; set; }
    public double R { get; set; }
}

public sealed class HollowTubeSectionDto : SectionDto
{
    public double D { get; set; }
    public double Thickness { get; set; }
}
