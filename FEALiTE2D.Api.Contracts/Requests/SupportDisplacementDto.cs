using FEALiTE2D.Api.Contracts.Enums;

namespace FEALiTE2D.Api.Contracts.Requests;

public sealed class SupportDisplacementDto
{
    public string NodeLabel { get; set; } = string.Empty;
    public double Ux { get; set; }
    public double Uy { get; set; }
    public double Rz { get; set; }
    public LoadDirectionDto Direction { get; set; } = LoadDirectionDto.Global;
}
