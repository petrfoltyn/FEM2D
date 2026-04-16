using FEALiTE2D.Api.Contracts.Enums;

namespace FEALiTE2D.Api.Contracts.Requests;

public sealed class NodalLoadDto
{
    public string NodeLabel { get; set; } = string.Empty;
    public double Fx { get; set; }
    public double Fy { get; set; }
    public double Mz { get; set; }
    public LoadDirectionDto Direction { get; set; } = LoadDirectionDto.Global;
}
