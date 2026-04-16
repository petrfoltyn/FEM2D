using System.Text.Json.Serialization;
using FEALiTE2D.Api.Contracts.Enums;

namespace FEALiTE2D.Api.Contracts.Requests;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(FrameElementDto), typeDiscriminator: "Frame")]
[JsonDerivedType(typeof(SpringElementDto), typeDiscriminator: "Spring")]
public abstract class ElementDto
{
    public string Label { get; set; } = string.Empty;
    public string StartNodeLabel { get; set; } = string.Empty;
    public string EndNodeLabel { get; set; } = string.Empty;
}

public sealed class FrameElementDto : ElementDto
{
    public string SectionLabel { get; set; } = string.Empty;
    public EndReleaseDto EndRelease { get; set; } = EndReleaseDto.NoRelease;
}

public sealed class SpringElementDto : ElementDto
{
    [JsonPropertyName("K")] public double K { get; set; }
    [JsonPropertyName("R")] public double R { get; set; }
}
