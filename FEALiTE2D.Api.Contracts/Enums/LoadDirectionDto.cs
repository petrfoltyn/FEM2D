using System.Text.Json.Serialization;

namespace FEALiTE2D.Api.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LoadDirectionDto
{
    Global = 0,
    Local
}
