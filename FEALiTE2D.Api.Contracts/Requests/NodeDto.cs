namespace FEALiTE2D.Api.Contracts.Requests;

public sealed class NodeDto
{
    public string Label { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double RotationAngle { get; set; }
    public SupportDto? Support { get; set; }
}
