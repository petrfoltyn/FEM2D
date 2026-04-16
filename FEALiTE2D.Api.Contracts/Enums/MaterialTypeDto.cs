using System.Text.Json.Serialization;

namespace FEALiTE2D.Api.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaterialTypeDto
{
    Concrete = 0,
    Steel,
    Timber,
    Aluminum,
    Userdefined
}
