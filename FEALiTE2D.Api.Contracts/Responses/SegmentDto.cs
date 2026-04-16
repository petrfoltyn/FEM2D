namespace FEALiTE2D.Api.Contracts.Responses;

public sealed class SegmentDto
{
    public double X1 { get; set; }
    public double X2 { get; set; }
    public ForceDto StartForce { get; set; } = new();
    public ForceDto EndForce { get; set; } = new();
    public DisplacementDto StartDisplacement { get; set; } = new();
    public DisplacementDto EndDisplacement { get; set; } = new();
}
