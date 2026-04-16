using System.Text.Json.Serialization;

namespace FEALiTE2D.Api.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LoadCaseTypeDto
{
    SelfWeight = 0,
    Dead,
    Live,
    Wind,
    Seismic,
    Accidental,
    Shrinkage
}
