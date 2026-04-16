using System.Text.Json.Serialization;

namespace FEALiTE2D.Api.Contracts.Requests;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RigidSupportDto), typeDiscriminator: "Rigid")]
[JsonDerivedType(typeof(SpringSupportDto), typeDiscriminator: "Spring")]
public abstract class SupportDto
{
}

public sealed class RigidSupportDto : SupportDto
{
    public bool Ux { get; set; }
    public bool Uy { get; set; }
    public bool Rz { get; set; }
}

public sealed class SpringSupportDto : SupportDto
{
    public double Kx { get; set; }
    public double Ky { get; set; }
    public double Cz { get; set; }
}
