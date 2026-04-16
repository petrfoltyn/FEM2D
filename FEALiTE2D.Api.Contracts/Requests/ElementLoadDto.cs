using System.Text.Json.Serialization;
using FEALiTE2D.Api.Contracts.Enums;

namespace FEALiTE2D.Api.Contracts.Requests;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PointLoadDto), typeDiscriminator: "Point")]
[JsonDerivedType(typeof(UniformLoadDto), typeDiscriminator: "Uniform")]
[JsonDerivedType(typeof(TrapezoidalLoadDto), typeDiscriminator: "Trapezoidal")]
public abstract class ElementLoadDto
{
    public string ElementLabel { get; set; } = string.Empty;
    public LoadDirectionDto Direction { get; set; } = LoadDirectionDto.Global;
}

public sealed class PointLoadDto : ElementLoadDto
{
    public double Fx { get; set; }
    public double Fy { get; set; }
    public double Mz { get; set; }
    public double L1 { get; set; }
}

public sealed class UniformLoadDto : ElementLoadDto
{
    public double Wx { get; set; }
    public double Wy { get; set; }
    public double L1 { get; set; }
    public double L2 { get; set; }
}

public sealed class TrapezoidalLoadDto : ElementLoadDto
{
    public double Wx1 { get; set; }
    public double Wx2 { get; set; }
    public double Wy1 { get; set; }
    public double Wy2 { get; set; }
    public double L1 { get; set; }
    public double L2 { get; set; }
}
